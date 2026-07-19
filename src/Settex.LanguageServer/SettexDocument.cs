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
    private volatile Snapshot current;

    public SettexDocument(string uri, string text)
    {
        this.uri = uri;
        this.current = Parse(uri, text);
    }

    public string Uri => this.uri;
    public string Text => this.current.Text;
    public IReadOnlyList<Token> Tokens => this.current.Tokens;
    public FileNode? Ast => this.current.Ast;
    public IReadOnlyList<Diagnostic> Diagnostics => this.current.Diagnostics;

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
        this.current = Parse(this.uri, newText);
    }

    /// <summary>
    /// Snapshot immuable de l'état analysé d'un document. <see cref="FilePath"/> est
    /// le chemin FS résolu du document (null s'il n'est pas enregistré) — il permet
    /// de distinguer les nœuds propres au fichier de ceux issus d'un <c>include</c>.
    /// </summary>
    internal sealed record Snapshot(
        string Text,
        IReadOnlyList<Token> Tokens,
        FileNode? Ast,
        IReadOnlyList<Diagnostic> Diagnostics,
        string? FilePath);

    /// <summary>
    /// Analyse complète du document (Lexer + Parser + résolution des includes)
    /// et production d'un snapshot immuable.
    /// </summary>
    private static Snapshot Parse(string uri, string text)
    {
        var diagnostics = new List<Diagnostic>();
        IReadOnlyList<Token> tokens = Array.Empty<Token>();
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
                try
                {
                    var includeResolver = new IncludeResolver();
                    var resolvedStatements = includeResolver.ResolveIncludes(parsedAst, filePath);

                    // Rebuild AST with resolved includes
                    ast = new FileNode(resolvedStatements, parsedAst.Location);
                }
                catch (IncludeException ex)
                {
                    // Include resolution failed, use original AST and report error
                    ast = parsedAst;
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

        return new Snapshot(text, tokens, ast, diagnostics, filePath);
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

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.GetFullPath(location.FilePath),
            Path.GetFullPath(documentFilePath),
            comparison);
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
    /// Convertit une SourceLocation en LSP Range.
    /// </summary>
    public static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range LocationToRange(SourceLocation location)
    {
        // LSP uses 0-based line/column
        var line = location.Line - 1;
        var column = location.Column - 1;
        return new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
            new Position(line, column),
            new Position(line, column + location.Length)
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
