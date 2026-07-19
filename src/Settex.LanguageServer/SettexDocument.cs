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
        this.current = Parse(this.uri, newText, this.includeContentProvider);
    }

    /// <summary>
    /// Re-analyses the document with its current text. Used when an included file
    /// changed, so the document picks up the new content.
    /// </summary>
    public void Refresh()
    {
        this.current = Parse(this.uri, this.current.Text, this.includeContentProvider);
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
                    var model = new Core.Evaluation.Evaluator().Evaluate(ast);

                    foreach (var coverage in Settex.Compilation.CoverageAnalyzer.Analyze(model))
                    {
                        diagnostics.Add(ToLspDiagnostic(coverage));
                    }

                    foreach (var layering in Settex.Compilation.ArrayLayeringAnalyzer.Analyze(model))
                    {
                        diagnostics.Add(ToLspDiagnostic(layering));
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
        var uri = string.IsNullOrEmpty(location.FilePath)
            ? currentUri
            : DocumentUri.FromFileSystemPath(location.FilePath);

        return new Location
        {
            Uri = uri,
            Range = LocationToRange(location),
        };
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
    /// cross-environment coverage warning) into an LSP diagnostic. Coverage
    /// diagnostics carry no source location, so they anchor at the file start.
    /// </summary>
    private static Diagnostic ToLspDiagnostic(Settex.Compilation.Diagnostic diagnostic) => new()
    {
        Range = diagnostic.Location != null ? LocationToRange(diagnostic.Location) : ZeroRange(),
        Severity = diagnostic.Severity switch
        {
            Settex.Compilation.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
            Settex.Compilation.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Information,
        },
        Code = diagnostic.Severity == Settex.Compilation.DiagnosticSeverity.Warning ? "STX501" : "STX401",
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
