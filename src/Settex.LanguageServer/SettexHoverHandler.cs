using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer;

/// <summary>
/// Handler pour les informations de survol (Hover).
/// Affiche la documentation des keywords, valeurs des variables, etc.
/// </summary>
public class SettexHoverHandler : HoverHandlerBase
{
    private readonly SettexWorkspace workspace;
    private readonly ILogger<SettexHoverHandler> logger;
    private readonly TextDocumentSelector documentSelector = new(
        new TextDocumentFilter { Pattern = "**/*.settex" }
    );

    public SettexHoverHandler(SettexWorkspace workspace, ILogger<SettexHoverHandler> logger)
    {
        this.workspace = workspace;
        this.logger = logger;
    }

    public override Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var document = this.workspace.GetDocument(uri);

        if (document == null || document.Text == null)
        {
            return Task.FromResult<Hover?>(null);
        }

        // Récupérer le mot à la position
        var word = GetWordAtPosition(document.Text, request.Position);
        
        if (string.IsNullOrEmpty(word))
        {
            return Task.FromResult<Hover?>(null);
        }

        // Vérifier si c'est un keyword
        var keywordDoc = GetKeywordDocumentation(word);
        if (keywordDoc != null)
        {
            return Task.FromResult<Hover?>(new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = keywordDoc
                })
            });
        }

        // Vérifier si c'est une variable
        if (document.Ast != null)
        {
            var variable = FindVariable(document.Ast, word);
            if (variable != null)
            {
                return Task.FromResult<Hover?>(new Hover
                {
                    Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = $"**Variable** (global scope)\n\n`{word}`"
                    })
                });
            }

            // Vérifier si c'est un environnement
            var env = FindEnvironment(document.Ast, word);
            if (env != null)
            {
                return Task.FromResult<Hover?>(new Hover
                {
                    Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = $"**Environment overlay**\n\n`{word}`"
                    })
                });
            }
        }

        return Task.FromResult<Hover?>(null);
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(
        HoverCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions
        {
            DocumentSelector = this.documentSelector
        };
    }

    private static string GetWordAtPosition(string text, Position position)
    {
        var lines = text.Split('\n');
        if (position.Line >= lines.Length)
        {
            return string.Empty;
        }

        var line = lines[position.Line];
        if (position.Character >= line.Length)
        {
            return string.Empty;
        }

        // Trouver le début et la fin du mot
        var start = position.Character;
        var end = position.Character;

        while (start > 0 && IsWordChar(line[start - 1]))
        {
            start--;
        }

        while (end < line.Length && IsWordChar(line[end]))
        {
            end++;
        }

        return line.Substring(start, end - start);
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    private static string? GetKeywordDocumentation(string word)
    {
        return word switch
        {
            "settings" => "**`settings` block**\n\nDefines the base configuration. All environments inherit from this block.\n\n```settex\nsettings {\n    App.Name = \"MyApp\"\n}\n```",
            "env" => "**`env` block**\n\nDefines environment-specific overrides.\n\n```settex\nenv Production {\n    App.Debug = false\n}\n```",
            "let" => "**`let` statement**\n\nDeclares a variable for reuse.\n\n```settex\nlet baseUrl = \"https://api.example.com\"\n```",
            "include" => "**`include` statement**\n\nIncludes another Settex file.\n\n```settex\ninclude \"common.settex\"\n```",
            "for" => "**`for` loop**\n\nIterates over arrays.\n\n```settex\nfor service in services {\n    Services[service.Name].Port = service.Port\n}\n```",
            "if" => "**`if` expression**\n\nConditional expression.\n\n```settex\nlet value = if condition then \"yes\" else \"no\"\n```",
            "and" => "**`and` operator**\n\nLogical AND operator.\n\n```settex\nif isEnabled and isReady then \"active\" else \"inactive\"\n```",
            "or" => "**`or` operator**\n\nLogical OR operator.\n\n```settex\nif isDev or isTest then true else false\n```",
            "not" => "**`not` operator**\n\nLogical NOT operator.\n\n```settex\nif not isProduction then \"dev-mode\" else \"prod-mode\"\n```",
            ":=" => "**Set-if-missing operator**\n\nSets the value only if the path doesn't exist.\n\n```settex\nApp.Port := 8000  // Only if App.Port is not already set\n```",
            _ => null
        };
    }

    private static Core.Parser.Ast.LetNode? FindVariable(Core.Parser.Ast.FileNode ast, string name)
    {
        return ast.Statements
            .OfType<Core.Parser.Ast.LetNode>()
            .FirstOrDefault(let => let.Name == name);
    }

    private static Core.Parser.Ast.EnvBlockNode? FindEnvironment(Core.Parser.Ast.FileNode ast, string name)
    {
        return ast.Statements
            .OfType<Core.Parser.Ast.EnvBlockNode>()
            .FirstOrDefault(env => env.EnvironmentName == name);
    }
}
