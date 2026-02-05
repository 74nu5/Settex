using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Settex.Core.Diagnostics;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;
using Settex.Core.Resolution;

namespace Settex.LanguageServer;

/// <summary>
/// Représente un document Settex ouvert dans l'éditeur.
/// Gère l'analyse incrémentale (Lexer + Parser) et les diagnostics.
/// </summary>
public class SettexDocument
{
    private readonly string uri;
    private string text;
    private List<Token> tokens;
    private FileNode? ast;
    private List<Diagnostic> diagnostics;

    public SettexDocument(string uri, string text)
    {
        this.uri = uri;
        this.text = text;
        this.tokens = new List<Token>();
        this.diagnostics = new List<Diagnostic>();
        this.Reparse();
    }

    public string Uri => this.uri;
    public string Text => this.text;
    public IReadOnlyList<Token> Tokens => this.tokens;
    public FileNode? Ast => this.ast;
    public IReadOnlyList<Diagnostic> Diagnostics => this.diagnostics;

    /// <summary>
    /// Met à jour le texte du document et re-parse.
    /// </summary>
    public void Update(string newText)
    {
        this.text = newText;
        this.Reparse();
    }

    /// <summary>
    /// Re-parse le document complet (Lexer + Parser + Include resolution).
    /// </summary>
    private void Reparse()
    {
        this.diagnostics.Clear();
        this.tokens.Clear();
        this.ast = null;

        try
        {
            // Phase 1: Lexer
            var lexer = new Core.Lexer.Lexer(this.text, this.uri);
            this.tokens = lexer.Tokenize().ToList();

            // Phase 2: Parser
            var parser = new Parser(this.tokens);
            var parsedAst = parser.Parse();

            // Phase 2.5: Resolve includes (if file is saved to disk)
            var filePath = UriToFilePath(this.uri);
            if (filePath != null)
            {
                try
                {
                    var includeResolver = new IncludeResolver();
                    var resolvedStatements = includeResolver.ResolveIncludes(parsedAst, filePath);
                    
                    // Rebuild AST with resolved includes
                    this.ast = new FileNode(resolvedStatements, parsedAst.Location);
                }
                catch (IncludeException ex)
                {
                    // Include resolution failed, use original AST and report error
                    this.ast = parsedAst;
                    this.diagnostics.Add(new Diagnostic
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
                this.ast = parsedAst;
            }
        }
        catch (LexerException ex)
        {
            // Erreur de lexer
            this.diagnostics.Add(new Diagnostic
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
            this.diagnostics.Add(new Diagnostic
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
            this.diagnostics.Add(new Diagnostic
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
    /// Convertit une URI LSP en chemin de fichier système.
    /// </summary>
    private static string? UriToFilePath(string uri)
    {
        if (uri.StartsWith("file:///"))
        {
            // file:///d:/path/to/file.settex -> d:/path/to/file.settex
            var path = uri.Substring(8);
            
            // On Windows, convertir les / en \
            if (Path.DirectorySeparatorChar == '\\')
            {
                path = path.Replace('/', '\\');
            }
            
            return path;
        }
        
        return null;
    }
}
