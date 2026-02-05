using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Settex.Core.Evaluation;
using Settex.Core.Runtime;

namespace Settex.LanguageServer;

/// <summary>
/// Handler pour les informations de survol (Hover).
/// Affiche la documentation des keywords, valeurs des variables, etc.
/// </summary>
public class SettexHoverHandler : HoverHandlerBase
{
    private readonly SettexWorkspace workspace;
    private readonly ILogger<SettexHoverHandler> logger;
    private readonly ScopeResolver scopeResolver;
    private readonly TextDocumentSelector documentSelector = new(
        new TextDocumentFilter { Pattern = "**/*.settex" }
    );

    public SettexHoverHandler(SettexWorkspace workspace, ILogger<SettexHoverHandler> logger)
    {
        this.workspace = workspace;
        this.logger = logger;
        this.scopeResolver = new ScopeResolver();
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
            // Construire la hiérarchie des scopes
            var rootScope = this.scopeResolver.BuildScopeHierarchy(document.Ast);
            
            // Trouver le scope actif à la position du curseur
            var activeScope = this.scopeResolver.FindScopeAt(rootScope, request.Position);
            
            if (activeScope != null)
            {
                // Chercher la variable dans le scope actif (avec remontée aux parents)
                var variable = this.scopeResolver.FindVariableInScope(word, activeScope);
                
                if (variable != null)
                {
                    // Construire un scope d'évaluation avec toutes les variables du contexte
                    var evaluationScope = BuildEvaluationScope(activeScope);
                    
                    // Évaluer la valeur de la variable
                    var (value, error) = EvaluateVariable(variable, evaluationScope);
                    
                    // Formater le nom du scope pour affichage
                    var scopeName = FormatScopeName(activeScope);
                    var hoverText = FormatVariableHover(word, value, error, scopeName);

                    return Task.FromResult<Hover?>(new Hover
                    {
                        Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                        {
                            Kind = MarkupKind.Markdown,
                            Value = hoverText
                        })
                    });
                }
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

    /// <summary>
    /// Évalue une variable let et retourne sa valeur ou une erreur.
    /// </summary>
    private static (RuntimeValue? Value, string? Error) EvaluateVariable(Core.Parser.Ast.LetNode letNode, VariableScope scope)
    {
        try
        {
            // Créer un evaluator avec le scope fourni
            var evaluator = new ExpressionEvaluator(scope);

            // Évaluer l'expression de la variable
            var value = evaluator.Evaluate(letNode.Value);
            return (value, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// Construit un VariableScope pour l'évaluation en incluant toutes les variables du scope et de ses parents.
    /// </summary>
    private static VariableScope BuildEvaluationScope(ScopeInfo scopeInfo)
    {
        // Créer un scope vide
        var scope = new VariableScope();

        // Collecter les scopes de la racine jusqu'au scope actuel
        var scopeChain = new List<ScopeInfo>();
        var current = scopeInfo;
        while (current != null)
        {
            scopeChain.Add(current);
            current = current.Parent;
        }

        // Inverser pour avoir du global vers le local
        scopeChain.Reverse();

        // Évaluer et ajouter toutes les variables dans l'ordre
        foreach (var s in scopeChain)
        {
            foreach (var letNode in s.Variables)
            {
                try
                {
                    // Évaluer la variable avec le scope courant
                    var evaluator = new ExpressionEvaluator(scope);
                    var value = evaluator.Evaluate(letNode.Value);
                    scope.Define(letNode.Name, value);
                }
                catch
                {
                    // Si l'évaluation échoue, définir comme null
                    scope.Define(letNode.Name, NullValue.Instance);
                }
            }
        }

        return scope;
    }

    /// <summary>
    /// Formate le nom d'un scope pour affichage.
    /// </summary>
    private static string FormatScopeName(ScopeInfo scopeInfo)
    {
        return scopeInfo.Type switch
        {
            ScopeType.Global => "Global",
            ScopeType.Env => $"Env \"{scopeInfo.Name}\"",
            ScopeType.ForLoop => $"For loop (iterator: {scopeInfo.Name})",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Formate les informations d'une variable pour le hover (Markdown).
    /// </summary>
    private static string FormatVariableHover(string name, RuntimeValue? value, string? error, string scope)
    {
        var result = $"**Variable:** `{name}`\n\n";
        result += $"**Scope:** {scope}\n\n";

        if (error != null)
        {
            result += $"**Error:** {error}";
        }
        else if (value != null)
        {
            result += $"**Type:** {GetValueTypeName(value)}\n\n";
            result += $"**Value:**\n```settex\n{FormatRuntimeValue(value)}\n```";
        }
        else
        {
            result += "**Value:** *(not evaluated)*";
        }

        return result;
    }

    /// <summary>
    /// Détermine le nom du type d'une RuntimeValue.
    /// </summary>
    private static string GetValueTypeName(RuntimeValue value)
    {
        return value switch
        {
            StringValue => "String",
            NumberValue => "Number",
            BoolValue => "Boolean",
            NullValue => "Null",
            ArrayValue => "Array",
            ObjectValue => "Object",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Formate une RuntimeValue en texte lisible pour affichage dans le hover.
    /// </summary>
    private static string FormatRuntimeValue(RuntimeValue value, int maxDepth = 2, int currentDepth = 0)
    {
        return value switch
        {
            // Primitives
            NumberValue num => num.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StringValue str => $"\"{EscapeString(str.Value)}\"",
            BoolValue b => b.Value ? "true" : "false",
            NullValue => "null",

            // Arrays
            ArrayValue arr when arr.Items.Count == 0 => "[]",
            ArrayValue arr when currentDepth >= maxDepth => $"[... {arr.Items.Count} items ...]",
            ArrayValue arr => FormatArray(arr, maxDepth, currentDepth),

            // Objects
            ObjectValue obj when obj.Properties.Count == 0 => "{}",
            ObjectValue obj when currentDepth >= maxDepth => $"{{ ... {obj.Properties.Count} properties ... }}",
            ObjectValue obj => FormatObject(obj, maxDepth, currentDepth),

            _ => "(unknown)"
        };
    }

    /// <summary>
    /// Formate un ArrayValue avec limitation d'items.
    /// </summary>
    private static string FormatArray(ArrayValue arr, int maxDepth, int currentDepth)
    {
        const int maxItems = 5;
        var items = arr.Items.Take(maxItems).Select(el => FormatRuntimeValue(el, maxDepth, currentDepth + 1));
        var formatted = string.Join(", ", items);

        if (arr.Items.Count > maxItems)
        {
            formatted += $", ... ({arr.Items.Count - maxItems} more)";
        }

        return $"[{formatted}]";
    }

    /// <summary>
    /// Formate un ObjectValue avec limitation de clés.
    /// </summary>
    private static string FormatObject(ObjectValue obj, int maxDepth, int currentDepth)
    {
        const int maxKeys = 3;
        var properties = obj.Properties.Take(maxKeys)
            .Select(kvp => $"\"{EscapeString(kvp.Key)}\": {FormatRuntimeValue(kvp.Value, maxDepth, currentDepth + 1)}");

        var formatted = string.Join(", ", properties);

        if (obj.Properties.Count > maxKeys)
        {
            formatted += $", ... ({obj.Properties.Count - maxKeys} more)";
        }

        return $"{{ {formatted} }}";
    }

    /// <summary>
    /// Échappe les caractères spéciaux dans une chaîne pour affichage.
    /// </summary>
    private static string EscapeString(string str)
    {
        return str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
