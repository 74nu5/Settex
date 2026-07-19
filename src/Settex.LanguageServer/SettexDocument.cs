using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Settex.Core.Diagnostics;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;
using Settex.Core.Resolution;

namespace Settex.LanguageServer;

/// <summary>
/// Représente un document Settex ouvert dans l'éditeur.
/// Gère l'analyse (Lexer + Parser + résolution des includes) et les diagnostics.
///
/// Thread-safety : l'état analysé est conservé dans un <see cref="Snapshot"/>
/// immuable référencé par un champ <c>volatile</c>. <see cref="Update"/> calcule
/// un nouveau snapshot et remplace la référence de façon atomique, si bien que les
/// handlers (completion, hover, …) qui lisent le document en parallèle voient
/// toujours un état cohérent, jamais partiellement mis à jour.
/// </summary>
public class SettexDocument
{
    private readonly string uri;
    private readonly Func<string, string?>? includeContentProvider;

    /// <summary>Serialises the writers. Readers stay lock-free via the volatile field.</summary>
    private readonly object writeLock = new();

    private volatile Snapshot current;

    /// <summary>
    /// Creates a document. <paramref name="includeContentProvider"/> lets the host
    /// resolve <c>include</c>s against open (possibly unsaved) buffers instead of the
    /// on-disk copy; returning <c>null</c> falls back to disk.
    /// </summary>
    public SettexDocument(string uri, string text, Func<string, string?>? includeContentProvider = null)
    {
        this.uri = uri;
        this.includeContentProvider = includeContentProvider;
        this.current = Parse(uri, text, includeContentProvider);
    }

    public string Uri => this.uri;
    public string Text => this.current.Text;
    public IReadOnlyList<Token> Tokens => this.current.Tokens;
    public FileNode? Ast => this.current.Ast;
    public IReadOnlyList<Diagnostic> Diagnostics => this.current.Diagnostics;

    /// <summary>The document's resolved filesystem path, or null when unsaved.</summary>
    public string? FilePath => this.current.FilePath;

    /// <summary>
    /// Every file this document was analysed from: itself plus all transitively
    /// included files. Used to re-analyse the document when one of them changes.
    /// </summary>
    public IReadOnlyCollection<string> Includes => this.current.Includes;

    /// <summary>
    /// The current immutable snapshot. Capture this once when a handler needs a
    /// consistent view across several properties (e.g. Text and Ast together),
    /// rather than reading the individual properties, which may observe different
    /// snapshots if the document is updated concurrently.
    /// </summary>
    internal Snapshot Current => this.current;

    /// <summary>
    /// Met à jour le texte du document et re-parse, en remplaçant atomiquement
    /// le snapshot courant.
    /// </summary>
    public void Update(string newText)
    {
        lock (this.writeLock)
        {
            this.current = Parse(this.uri, newText, this.includeContentProvider);
        }
    }

    /// <summary>
    /// Re-analyses the document with its current text. Used when an included file
    /// changed, so the document picks up the new content.
    /// <para>
    /// Serialised with <see cref="Update" />: unlike it, this is a read-modify-write —
    /// it re-reads the current text — and the two now have different callers. Update
    /// comes from the document's own didChange, Refresh from a change to a file it
    /// includes. Interleaved, Refresh could re-parse text captured before an Update
    /// and publish it, silently rewinding the server's copy of the buffer.
    /// </para>
    /// </summary>
    public void Refresh()
    {
        lock (this.writeLock)
        {
            this.current = Parse(this.uri, this.current.Text, this.includeContentProvider);
        }
    }

    /// <summary>
    /// Snapshot immuable de l'état analysé d'un document. <see cref="FilePath"/> est
    /// le chemin FS résolu du document (null s'il n'est pas enregistré) — il permet
    /// de distinguer les nœuds propres au fichier de ceux issus d'un <c>include</c>.
    /// <see cref="Includes"/> liste les fichiers dont l'analyse dépend.
    /// </summary>
    internal sealed record Snapshot(
        string Text,
        IReadOnlyList<Token> Tokens,
        FileNode? Ast,
        IReadOnlyList<Diagnostic> Diagnostics,
        string? FilePath,
        IReadOnlyCollection<string> Includes);

    /// <summary>
    /// Analyse complète du document (Lexer + Parser + résolution des includes)
    /// et production d'un snapshot immuable.
    /// </summary>
    private static Snapshot Parse(string uri, string text, Func<string, string?>? includeContentProvider)
    {
        var diagnostics = new List<Diagnostic>();
        IReadOnlyList<Token> tokens = Array.Empty<Token>();
        IReadOnlyCollection<string> includes = Array.Empty<string>();
        FileNode? ast = null;

        // Resolve the real filesystem path up front so every token/AST node of the
        // main file carries it (or null for an unsaved document). This keeps the
        // FilePath consistent with the included files' nodes, which lets
        // go-to-definition point at the right file.
        var filePath = UriToFilePath(uri);

        try
        {
            // Phase 1: Lexer
            var lexer = new Core.Lexer.Lexer(text, filePath);
            tokens = lexer.Tokenize().ToList();

            // Phase 2: Parser
            var parser = new Parser(tokens.ToList(), filePath);
            var parsedAst = parser.Parse();

            // Phase 2.5: Resolve includes (if file is saved to disk)
            if (filePath != null)
            {
                var includeResolver = new IncludeResolver(includeContentProvider);

                try
                {
                    var resolvedStatements = includeResolver.ResolveIncludes(parsedAst, filePath);

                    // Rebuild AST with resolved includes
                    ast = new FileNode(resolvedStatements, parsedAst.Location);
                    includes = includeResolver.ResolvedFiles.ToList();
                }
                catch (IncludeException ex)
                {
                    // Include resolution failed, use original AST and report error.
                    // Keep whatever was resolved so a later fix to one of those files
                    // still triggers a re-analysis of this document.
                    ast = parsedAst;
                    includes = includeResolver.ResolvedFiles.ToList();
                    diagnostics.Add(new Diagnostic
                    {
                        Range = ex.Location != null ? LocationToRange(ex.Location) : new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(new Position(0, 0), new Position(0, 0)),
                        Severity = DiagnosticSeverity.Error,
                        Code = "STX301",
                        Source = "settex",
                        Message = ex.Message
                    });
                }
            }
            else
            {
                // File not on disk (unsaved), use original AST without includes
                ast = parsedAst;
            }

            // Phase 3: evaluate to surface semantic errors, and run the coverage
            // check so cross-environment drift shows up in the editor — not only at
            // the CLI/build. Skipped when earlier phases already reported an error
            // (evaluating a half-resolved file would produce spurious diagnostics).
            if (ast != null && diagnostics.Count == 0)
            {
                try
                {
                    // The CLI only ever compiles a root file; the editor opens whatever
                    // the user opens. An include fragment — the recommended way to share
                    // configuration — has no 'settings' block by design, so requiring one
                    // here would put a permanent red squiggle on a perfectly valid file.
                    var model = new Core.Evaluation.Evaluator().Evaluate(ast, requireSettingsBlock: false);

                    // Drift analysis needs the whole picture. A fragment only holds part
                    // of it — the environments it says nothing about may well be declared
                    // by the file that includes it — so comparing environments here would
                    // report drift that does not exist.
                    if (HasSettingsBlock(ast))
                    {
                        var analyses = Settex.Compilation.CoverageAnalyzer.Analyze(model)
                            .Concat(Settex.Compilation.ArrayLayeringAnalyzer.Analyze(model));

                        foreach (var analysis in analyses)
                        {
                            // Anchor on the assignment that introduced the key. The
                            // analyzers work on the evaluated JSON, which has no source
                            // positions, so every one of these used to land on the first
                            // character — a dozen warnings stacked on one spot, none of
                            // them pointing at what to change.
                            var anchor = FindAssignmentLocation(ast, analysis.KeyPath, analysis.EnvironmentName, filePath);

                            // No anchor means the key lives in an included file. It is
                            // reported there, on the assignment itself, rather than here
                            // where the reader cannot act on it — the AST is
                            // include-flattened, so without this every includer
                            // republished its includes' warnings verbatim.
                            if (analysis.KeyPath != null && anchor == null)
                            {
                                continue;
                            }

                            diagnostics.Add(ToLspDiagnostic(analysis, anchor));
                        }
                    }
                }
                catch (Core.Evaluation.EvaluatorException ex)
                {
                    diagnostics.Add(SemanticError(ex.Message, ex.Location));
                }
                catch (Core.Merging.MergerException ex)
                {
                    diagnostics.Add(SemanticError(ex.Message, ex.Location));
                }
            }
        }
        catch (LexerException ex)
        {
            // Erreur de lexer
            diagnostics.Add(new Diagnostic
            {
                Range = LocationToRange(ex.Location),
                Severity = DiagnosticSeverity.Error,
                Code = "STX101",
                Source = "settex",
                Message = ex.Message
            });
        }
        catch (ParserException ex)
        {
            // Erreur de parser
            diagnostics.Add(new Diagnostic
            {
                Range = LocationToRange(ex.Location),
                Severity = DiagnosticSeverity.Error,
                Code = "STX201",
                Source = "settex",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            //Range inattendue
            diagnostics.Add(new Diagnostic
            {
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    new Position(0, 0),
                    new Position(0, 0)
                ),
                Severity = DiagnosticSeverity.Error,
                Code = "STX999",
                Source = "settex",
                Message = $"Unexpected error: {ex.Message}"
            });
        }

        return new Snapshot(text, tokens, ast, diagnostics, filePath, includes);
    }

    /// <summary>
    /// Indique si le fichier produit effectivement de la configuration, c'est-à-dire
    /// s'il ressemble à une racine de compilation plutôt qu'à un fragment destiné à
    /// être inclus.
    /// </summary>
    private static bool HasSettingsBlock(FileNode ast)
        => ast.Statements.OfType<SettingsBlockNode>().Any();

    /// <summary>
    /// Finds where a dotted configuration key is assigned <strong>in this document</strong>,
    /// searching the base settings and every environment overlay. Returns <c>null</c>
    /// when the key is not assigned here — either because it comes from an included
    /// file, or because it is not an assignment at all.
    /// Keys are matched case-insensitively, as .NET configuration compares them.
    /// </summary>
    private static SourceLocation? FindAssignmentLocation(
        FileNode ast,
        string? keyPath,
        string? environmentName,
        string? documentFilePath)
    {
        if (string.IsNullOrEmpty(keyPath))
        {
            return null;
        }

        // The named environment's own block first. These diagnostics are about an
        // environment overriding or missing something, so its assignment is the one to
        // point at — the base may assign the same key, and matching that instead would
        // send the reader to a line that is not the problem.
        var inEnvironment = Search(env => env.EnvironmentName == environmentName);

        return inEnvironment ?? Search(_ => true);

        SourceLocation? Search(Func<EnvBlockNode, bool> environmentFilter)
        {
            foreach (var statement in ast.Statements)
            {
                if (!IsFromSameFile(statement.Location, documentFilePath))
                {
                    continue;
                }

                var block = statement switch
                {
                    EnvBlockNode env when environmentFilter(env) => env.SettingsBlock.Block,
                    SettingsBlockNode settings when environmentName == null => settings.Block,
                    _ => null,
                };

                if (block == null)
                {
                    continue;
                }

                var found = FindAssignmentInBlock(block, prefix: string.Empty, keyPath);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }

    private static SourceLocation? FindAssignmentInBlock(BlockNode block, string prefix, string keyPath)
    {
        foreach (var statement in block.Statements)
        {
            switch (statement)
            {
                case AssignmentNode assignment:
                {
                    var path = Join(prefix, string.Join(".", assignment.Path.Segments));

                    if (string.Equals(path, keyPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return assignment.Location;
                    }

                    break;
                }

                case NestedBlockNode nested:
                {
                    var found = FindAssignmentInBlock(nested.Block, Join(prefix, nested.Name), keyPath);

                    if (found != null)
                    {
                        return found;
                    }

                    break;
                }
            }
        }

        return null;

        static string Join(string prefix, string segment)
            => prefix.Length == 0 ? segment : $"{prefix}.{segment}";
    }

    /// <summary>
    /// Indique si un symbole à la localisation donnée appartient <strong>à ce
    /// document</strong> (et non à un fichier inclus). Un nœud sans <c>FilePath</c>
    /// (document non enregistré) est considéré comme propre au document.
    /// </summary>
    public static bool IsFromSameFile(SourceLocation location, string? documentFilePath)
    {
        if (string.IsNullOrEmpty(location.FilePath) || string.IsNullOrEmpty(documentFilePath))
        {
            return true;
        }

        return SamePath(location.FilePath, documentFilePath);
    }

    /// <summary>
    /// Compares two filesystem paths using the host's case semantics
    /// (case-insensitive on Windows), after normalising them.
    /// </summary>
    public static bool SamePath(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
        }
        catch (Exception)
        {
            // Malformed path: fall back to a plain comparison rather than throwing.
            return string.Equals(left, right, comparison);
        }
    }

    /// <summary>
    /// Construit une LSP <see cref="Location"/> pour une <see cref="SourceLocation"/>,
    /// en pointant vers le <strong>fichier réel</strong> du symbole. Les nœuds issus
    /// d'un <c>include</c> portent le chemin du fichier inclus : sans cela, la
    /// navigation renverrait l'URI du document courant avec les lignes du fichier
    /// inclus (mauvais fichier). Retombe sur <paramref name="currentUri"/> quand la
    /// localisation n'a pas de chemin (document non enregistré).
    /// </summary>
    public static Location ToLspLocation(SourceLocation location, DocumentUri currentUri)
    {
        return new Location
        {
            Uri = ResolveTargetUri(location.FilePath, currentUri),
            Range = LocationToRange(location),
        };
    }

    /// <summary>
    /// Chooses the URI to report a target under. When the target is the current
    /// document, the request's own URI is reused verbatim rather than rebuilt from
    /// the filesystem path: a round-trip can change drive-letter casing or encoding
    /// on Windows, and a client that compares URIs would then treat the result as a
    /// different document (opening a new tab instead of revealing the position).
    /// </summary>
    private static DocumentUri ResolveTargetUri(string? targetFilePath, DocumentUri currentUri)
    {
        if (string.IsNullOrEmpty(targetFilePath))
        {
            return currentUri;
        }

        try
        {
            if (SamePath(targetFilePath, currentUri.GetFileSystemPath()))
            {
                return currentUri;
            }
        }
        catch (Exception)
        {
            // Non-file URI (untitled:, etc.): fall through to the path-based URI.
        }

        return DocumentUri.FromFileSystemPath(targetFilePath);
    }

    /// <summary>
    /// Builds an error diagnostic for a semantic (evaluation/merge) failure, at the
    /// given location or the file start when none is available.
    /// </summary>
    private static Diagnostic SemanticError(string message, SourceLocation? location) => new()
    {
        Range = location != null ? LocationToRange(location) : ZeroRange(),
        Severity = DiagnosticSeverity.Error,
        Code = "STX401",
        Source = "settex",
        Message = message,
    };

    /// <summary>
    /// Converts a compiler <see cref="Settex.Compilation.Diagnostic"/> (e.g. a
    /// cross-environment coverage warning) into an LSP diagnostic, anchored on the
    /// assignment named by <paramref name="anchor"/> when one was found.
    /// </summary>
    private static Diagnostic ToLspDiagnostic(Settex.Compilation.Diagnostic diagnostic, SourceLocation? anchor = null) => new()
    {
        Range = (diagnostic.Location ?? anchor) is { } location ? LocationToRange(location) : ZeroRange(),
        Severity = diagnostic.Severity switch
        {
            Settex.Compilation.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
            Settex.Compilation.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Information,
        },
        Code = diagnostic.Severity switch
        {
            Settex.Compilation.DiagnosticSeverity.Error => "STX401",
            Settex.Compilation.DiagnosticSeverity.Warning => "STX501",

            // Anything informational is not an error, and labelling it with the error
            // code made it look like one in the Problems panel.
            _ => "STX601",
        },
        Source = "settex",
        Message = diagnostic.Message,
    };

    private static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range ZeroRange()
        => new(new Position(0, 0), new Position(0, 0));

    /// <summary>
    /// Convertit une SourceLocation en LSP Range. Les nœuds multi-lignes (blocs,
    /// tableaux, fichier) portent désormais une vraie fin (<c>EndLine</c>/<c>EndColumn</c>),
    /// ce qui donne des ranges de symboles corrects et respecte l'inclusion
    /// parent/enfant exigée par LSP. Les positions sont bornées à zéro : le protocole
    /// n'accepte pas de coordonnées négatives.
    /// </summary>
    public static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range LocationToRange(SourceLocation location)
    {
        // LSP uses 0-based line/column; SourceLocation is 1-based.
        var startLine = Math.Max(0, location.Line - 1);
        var startColumn = Math.Max(0, location.Column - 1);
        var endLine = Math.Max(startLine, location.EffectiveEndLine - 1);
        var endColumn = Math.Max(0, location.EffectiveEndColumn - 1);

        // On a single line the end must not precede the start. The clamp above only
        // guards against a negative column, so a location whose end column came out
        // below its start produced an inverted range — which LSP does not define and
        // clients render arbitrarily. No node produces one today; this keeps it that
        // way rather than relying on that staying true.
        if (endLine == startLine)
        {
            endColumn = Math.Max(startColumn, endColumn);
        }

        return new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
            new Position(startLine, startColumn),
            new Position(endLine, endColumn)
        );
    }

    /// <summary>
    /// Convertit une URI LSP en chemin de fichier système, ou <c>null</c> si l'URI
    /// ne désigne pas un fichier sur disque (document non enregistré, schéma non
    /// <c>file</c>). Délègue à <see cref="DocumentUri"/>, qui décode l'URL (espaces
    /// <c>%20</c>, etc.) et gère les lettres de lecteur Windows et les chemins UNC —
    /// contrairement à l'ancien découpage manuel de chaîne.
    /// </summary>
    private static string? UriToFilePath(string uri)
    {
        try
        {
            var documentUri = DocumentUri.Parse(uri);

            if (!string.Equals(documentUri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var path = documentUri.GetFileSystemPath();
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch (Exception)
        {
            // URI malformée : traiter comme un document sans fichier sur disque.
            return null;
        }
    }
}
