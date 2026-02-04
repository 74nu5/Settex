using System.Collections.Generic;
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
/// Handler pour l'autocomplétion (IntelliSense).
/// Fournit des suggestions contextuelles : keywords, variables, environnements, etc.
/// </summary>
public class SettexCompletionHandler : CompletionHandlerBase
{
    private readonly SettexWorkspace workspace;
    private readonly ILogger<SettexCompletionHandler> logger;
    private readonly TextDocumentSelector documentSelector = new(
        new TextDocumentFilter { Pattern = "**/*.settex" }
    );

    // Keywords top-level
    private static readonly string[] TopLevelKeywords = { "settings", "env", "include", "let" };
    
    // Keywords dans expressions
    private static readonly string[] ExpressionKeywords = { "and", "or", "not", "if", "for", "true", "false", "null" };

    public SettexCompletionHandler(SettexWorkspace workspace, ILogger<SettexCompletionHandler> logger)
    {
        this.workspace = workspace;
        this.logger = logger;
    }

    public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var document = this.workspace.GetDocument(uri);

        if (document == null)
        {
            return Task.FromResult(new CompletionList());
        }

        var completions = new List<CompletionItem>();

        // Keywords top-level
        foreach (var keyword in TopLevelKeywords)
        {
            completions.Add(new CompletionItem
            {
                Label = keyword,
                Kind = CompletionItemKind.Keyword,
                Detail = $"{keyword} block",
                Documentation = GetKeywordDocumentation(keyword),
                InsertText = GetKeywordSnippet(keyword),
                InsertTextFormat = InsertTextFormat.Snippet
            });
        }

        // Keywords dans expressions
        foreach (var keyword in ExpressionKeywords)
        {
            completions.Add(new CompletionItem
            {
                Label = keyword,
                Kind = CompletionItemKind.Keyword,
                Detail = $"{keyword} operator",
                InsertText = keyword
            });
        }

        // Variables (extraction simple depuis l'AST)
        if (document.Ast != null)
        {
            var variables = ExtractVariables(document.Ast);
            foreach (var variable in variables)
            {
                completions.Add(new CompletionItem
                {
                    Label = variable,
                    Kind = CompletionItemKind.Variable,
                    Detail = "Variable",
                    InsertText = variable
                });
            }

            // Environnements
            var environments = ExtractEnvironments(document.Ast);
            foreach (var env in environments)
            {
                completions.Add(new CompletionItem
                {
                    Label = env,
                    Kind = CompletionItemKind.EnumMember,
                    Detail = "Environment",
                    InsertText = env
                });
            }
        }

        return Task.FromResult(new CompletionList(completions));
    }

    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
    {
        // Pas de résolution supplémentaire nécessaire
        return Task.FromResult(request);
    }

    protected override CompletionRegistrationOptions CreateRegistrationOptions(
        CompletionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new CompletionRegistrationOptions
        {
            DocumentSelector = this.documentSelector,
            TriggerCharacters = new[] { ".", "\"", "$", "{" },
            ResolveProvider = false
        };
    }

    private static string GetKeywordDocumentation(string keyword)
    {
        return keyword switch
        {
            "settings" => "Define configuration settings",
            "env" => "Define environment-specific overrides",
            "include" => "Include another Settex file",
            "let" => "Declare a variable",
            _ => ""
        };
    }

    private static string GetKeywordSnippet(string keyword)
    {
        return keyword switch
        {
            "settings" => "settings {\n\t$0\n}",
            "env" => "env ${1:Environment} {\n\t$0\n}",
            "include" => "include \"${1:file.settex}\"",
            "let" => "let ${1:name} = ${0:value}",
            _ => keyword
        };
    }

    private static List<string> ExtractVariables(Core.Parser.Ast.FileNode ast)
    {
        var variables = new List<string>();
        
        foreach (var stmt in ast.Statements)
        {
            if (stmt is Core.Parser.Ast.LetNode let)
            {
                variables.Add(let.Name);
            }
        }

        return variables.Distinct().ToList();
    }

    private static List<string> ExtractEnvironments(Core.Parser.Ast.FileNode ast)
    {
        var environments = new List<string>();
        
        foreach (var stmt in ast.Statements)
        {
            if (stmt is Core.Parser.Ast.EnvBlockNode env)
            {
                environments.Add(env.EnvironmentName);
            }
        }

        return environments.Distinct().ToList();
    }
}
