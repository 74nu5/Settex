using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Settex.Core.Evaluation;
using Settex.Core.Runtime;
using Settex.Core.Merging;

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

    /// <summary>
    /// Degrades to "no hover" instead of faulting the request: an unexpected
    /// failure in analysis should not surface as a broken LSP call in the editor.
    /// </summary>
    public override async Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        try
        {
            return await this.HandleCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Hover failed for {Uri}", request.TextDocument.Uri);
            return null;
        }
    }

    private Task<Hover?> HandleCoreAsync(HoverParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var document = this.workspace.GetDocument(uri);

        if (document == null)
        {
            return Task.FromResult<Hover?>(null);
        }

        // Capturer un seul snapshot immuable : le texte (pour l'offset du curseur)
        // et l'AST restent cohérents même si le document est modifié en parallèle.
        var snapshot = document.Current;

        // Récupérer le mot à la position
        var word = GetWordAtPosition(snapshot.Text, request.Position);

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
        if (snapshot.Ast != null)
        {
            // PREMIÈRE PRIORITÉ : Vérifier si le curseur est sur un path d'assignation (overlay tracking)
            // MAIS seulement si le mot sous le curseur correspond au path !
            var assignmentInfo = FindAssignmentAtPosition(snapshot.Ast, request.Position);
            if (assignmentInfo != null)
            {
                // Le chemin complet inclut les blocs imbriqués traversés, donc
                // survoler "Port" dans `Server { Port = … }` cible bien "Server.Port".
                var (_, pathSegments, envName) = assignmentInfo.Value;

                // Vérifier que le mot sous le curseur est bien le path (ou une partie du path)
                var isOnPath = pathSegments.Any(segment => segment == word);

                if (isOnPath)
                {
                    var path = string.Join(".", pathSegments);

                    // Détecter si le mot survolé est un segment d'objet (pas le dernier segment)
                    var wordIndex = pathSegments.IndexOf(word);
                    var isObjectSegment = wordIndex >= 0 && wordIndex < pathSegments.Count - 1;

                    if (isObjectSegment)
                    {
                        // Le mot survolé est un objet (ex: "Server" dans "Server.Port")
                        // Construire le path de l'objet (tous les segments jusqu'au mot inclus)
                        var objectPath = string.Join(".", pathSegments.Take(wordIndex + 1));
                        this.logger.LogTrace("[HOVER-OVERLAY] Formatting OBJECT for path='{ObjectPath}', envName='{EnvName}', word='{Word}'", objectPath, envName ?? "(base)", word);
                        
                        var objectHover = HoverOverlayFormatter.FormatObjectWithOverlay(snapshot.Ast, objectPath, envName, this.logger);
                        
                        this.logger.LogTrace("[HOVER-OVERLAY] Object result: {IsNull}, length={Length}", objectHover == null ? "NULL" : "NOT NULL", objectHover?.Length ?? 0);
                        if (objectHover != null)
                        {
                            this.logger.LogTrace("[HOVER-OVERLAY] Object content:\n{Content}", objectHover);
                            return Task.FromResult<Hover?>(new Hover
                            {
                                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                                {
                                    Kind = MarkupKind.Markdown,
                                    Value = objectHover
                                })
                            });
                        }
                    }
                    else
                    {
                        // Le mot survolé est la valeur finale (ex: "Port" dans "Server.Port")
                        this.logger.LogTrace("[HOVER-OVERLAY] Formatting assignment for path='{Path}', envName='{EnvName}', word='{Word}'", path, envName ?? "(base)", word);
                        
                        var overlayHover = HoverOverlayFormatter.FormatAssignmentWithOverlay(snapshot.Ast, path, envName, this.logger);
                        
                        this.logger.LogTrace("[HOVER-OVERLAY] Result: {IsNull}, length={Length}", overlayHover == null ? "NULL" : "NOT NULL", overlayHover?.Length ?? 0);
                        if (overlayHover != null)
                        {
                            this.logger.LogTrace("[HOVER-OVERLAY] Content:\n{Content}", overlayHover);
                        }
                        
                        if (overlayHover != null)
                        {
                            return Task.FromResult<Hover?>(new Hover
                            {
                                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                                {
                                    Kind = MarkupKind.Markdown,
                                    Value = overlayHover
                                })
                            });
                        }
                    }
                }
            }

            // DEUXIÈME PRIORITÉ : Construire la hiérarchie des scopes
            var rootScope = this.scopeResolver.BuildScopeHierarchy(snapshot.Ast);

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

                    // Un itérateur de boucle est une variable implicite : sa "valeur"
                    // est la collection parcourue, pas l'élément courant — le hover le
                    // dit explicitement plutôt que de laisser croire l'inverse.
                    var hoverText = IsLoopIterator(activeScope, word)
                        ? FormatIteratorHover(word, value, error, scopeName)
                        : FormatVariableHover(word, value, error, scopeName);

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
            var env = FindEnvironment(snapshot.Ast, word);
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
    /// Indique si un nom résolu est l'itérateur d'une boucle for englobante.
    /// </summary>
    private static bool IsLoopIterator(ScopeInfo? scope, string name)
    {
        for (var current = scope; current != null; current = current.Parent)
        {
            if (current.Type == ScopeType.ForLoop && current.Name == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Formate le hover d'un itérateur de boucle : il est lié tour à tour à chaque
    /// élément, donc on montre la collection parcourue plutôt qu'une "valeur".
    /// </summary>
    private static string FormatIteratorHover(string name, RuntimeValue? collection, string? error, string scope)
    {
        var result = $"**Loop iterator:** `{name}`\n\n";
        result += $"**Scope:** {scope}\n\n";
        result += "Bound to each element of the collection in turn.\n\n";

        if (error != null)
        {
            result += $"**Error:** {error}";
        }
        else if (collection != null)
        {
            result += $"**Iterates over:**\n```settex\n{FormatRuntimeValue(collection)}\n```";
        }

        return result;
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

    /// <summary>
    /// Trouve une assignation à la position donnée et retourne l'assignation + l'environnement (null si dans base).
    /// </summary>
    private static (Core.Parser.Ast.AssignmentNode Assignment, List<string> Path, string? EnvName)? FindAssignmentAtPosition(
        Core.Parser.Ast.FileNode ast,
        Position position)
    {
        // Convertir position LSP (0-based) en position Settex (1-based)
        var line = position.Line + 1;
        var column = position.Character + 1;

        // Chercher dans les statements de base
        foreach (var stmt in ast.Statements)
        {
            if (stmt is Core.Parser.Ast.SettingsBlockNode settings)
            {
                var found = FindAssignmentInStatements(settings.Block.Statements, new List<string>(), line, column);
                if (found != null)
                {
                    return (found.Value.Assignment, found.Value.Path, null); // Base, pas d'environnement
                }
            }
            else if (stmt is Core.Parser.Ast.EnvBlockNode env)
            {
                var found = FindAssignmentInStatements(env.SettingsBlock.Block.Statements, new List<string>(), line, column);
                if (found != null)
                {
                    return (found.Value.Assignment, found.Value.Path, env.EnvironmentName); // Dans un environnement
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Cherche récursivement une assignation à une position donnée, en descendant
    /// dans les blocs imbriqués. Le préfixe accumule les noms de blocs traversés,
    /// afin que l'assignation soit rapportée avec son <strong>chemin complet</strong>
    /// (<c>Server.Port</c> et non <c>Port</c>) — c'est ce chemin que l'overlay
    /// recherche dans la configuration évaluée.
    /// </summary>
    private static (Core.Parser.Ast.AssignmentNode Assignment, List<string> Path)? FindAssignmentInStatements(
        IReadOnlyList<Core.Parser.Ast.IStatement> statements,
        List<string> prefix,
        int line,
        int column)
    {
        foreach (var stmt in statements)
        {
            if (stmt is Core.Parser.Ast.AssignmentNode assignment)
            {
                if (IsPositionInLocation(assignment.Location, line, column))
                {
                    var fullPath = new List<string>(prefix);
                    fullPath.AddRange(assignment.Path.Segments);
                    return (assignment, fullPath);
                }
            }
            else if (stmt is Core.Parser.Ast.NestedBlockNode nested)
            {
                var nestedPrefix = new List<string>(prefix) { nested.Name };
                var found = FindAssignmentInStatements(nested.Block.Statements, nestedPrefix, line, column);

                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Vérifie si une position (1-based) est réellement dans l'étendue d'un nœud.
    /// Les assignations portent désormais une étendue allant du chemin à la fin de
    /// leur valeur, donc survoler ailleurs sur la même ligne ne déclenche plus
    /// l'overlay.
    /// </summary>
    private static bool IsPositionInLocation(Core.Diagnostics.SourceLocation location, int line, int column)
    {
        if (line < location.Line || line > location.EffectiveEndLine)
        {
            return false;
        }

        if (line == location.Line && column < location.Column)
        {
            return false;
        }

        if (line == location.EffectiveEndLine && column > location.EffectiveEndColumn)
        {
            return false;
        }

        return true;
    }
}
