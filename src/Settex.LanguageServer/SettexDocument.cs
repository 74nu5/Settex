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
    /// Snapshot immuable de l'état analysé d'un document.
    /// </summary>
    internal sealed record Snapshot(
        string Text,
        IReadOnlyList<Token> Tokens,
        FileNode? Ast,
        IReadOnlyList<Diagnostic> Diagnostics);

    /// <summary>
    /// Analyse complète du document (Lexer + Parser + résolution des includes)
    /// et production d'un snapshot immuable.
    /// </summary>
    private static Snapshot Parse(string uri, string text)
    {
        var diagnostics = new List<Diagnostic>();
        IReadOnlyList<Token> tokens = Array.Empty<Token>();
        FileNode? ast = null;

        try
        {
            // Phase 1: Lexer
            var lexer = new Core.Lexer.Lexer(text, uri);
            tokens = lexer.Tokenize().ToList();

            // Phase 2: Parser
            var parser = new Parser(tokens.ToList());
            var parsedAst = parser.Parse();

            // Phase 2.5: Resolve includes (if file is saved to disk)
            var filePath = UriToFilePath(uri);
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

        return new Snapshot(text, tokens, ast, diagnostics);
    }

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
