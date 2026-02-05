using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Settex.Core.Evaluation;
using Settex.Core.Merging;
using Settex.Core.Parser.Ast;

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

        // Détection du contexte : est-on après un point pour une propriété d'objet ?
        var textBeforeCursor = this.GetTextBeforeCursor(document, request.Position);
        var objectPath = this.ExtractObjectPath(textBeforeCursor);

        // Extraire le mot partiel en cours de frappe pour le filtrage
        var partialWord = this.ExtractPartialWord(textBeforeCursor);
        this.logger.LogInformation("[COMPLETION] Partial word: '{PartialWord}'", partialWord ?? "(none)");

        if (!string.IsNullOrEmpty(objectPath))
        {
            // Autocomplétion des propriétés d'objet
            this.logger.LogInformation("[COMPLETION] Object property completion for path: {Path}", objectPath);
            var properties = this.GetObjectProperties(document, objectPath);
            
            foreach (var (propertyName, environments) in properties)
            {
                // Filtrer par le mot partiel si présent
                if (!string.IsNullOrEmpty(partialWord) && 
                    !propertyName.StartsWith(partialWord, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var envList = string.Join(", ", environments);
                completions.Add(new CompletionItem
                {
                    Label = propertyName,
                    Kind = CompletionItemKind.Property,
                    Detail = $"Property (in {envList})",
                    Documentation = $"Available in environments: {envList}",
                    InsertText = propertyName,
                    FilterText = propertyName
                });
            }

            // Si on a des propriétés, on retourne uniquement celles-ci
            if (completions.Count > 0)
            {
                this.logger.LogInformation("[COMPLETION] Returning {Count} property completions", completions.Count);
                return Task.FromResult(new CompletionList(completions));
            }
        }

        // Autocomplétion générale : keywords, variables, environments, et noms d'objets existants

        // Proposer les noms d'objets existants en priorité
        this.logger.LogInformation("[COMPLETION] Adding existing object names");
        var objectNames = this.GetAllObjectNames(document);
        
        foreach (var (objectName, environments) in objectNames)
        {
            // Filtrer par le mot partiel si présent
            if (!string.IsNullOrEmpty(partialWord) && 
                !objectName.StartsWith(partialWord, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var envList = string.Join(", ", environments);
            completions.Add(new CompletionItem
            {
                Label = objectName,
                Kind = CompletionItemKind.Class,
                Detail = $"Object (defined in {envList})",
                Documentation = $"Existing object defined in: {envList}",
                InsertText = objectName,
                FilterText = objectName,
                SortText = "0_" + objectName // Priorité haute pour les objets
            });
        }

        // Sinon, autocomplétion générale : keywords, variables, environments

        // Keywords top-level
        foreach (var keyword in TopLevelKeywords)
        {
            // Filtrer par le mot partiel
            if (!string.IsNullOrEmpty(partialWord) && 
                !keyword.StartsWith(partialWord, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            completions.Add(new CompletionItem
            {
                Label = keyword,
                Kind = CompletionItemKind.Keyword,
                Detail = $"{keyword} block",
                Documentation = GetKeywordDocumentation(keyword),
                InsertText = GetKeywordSnippet(keyword),
                InsertTextFormat = InsertTextFormat.Snippet,
                FilterText = keyword
            });
        }

        // Keywords dans expressions
        foreach (var keyword in ExpressionKeywords)
        {
            // Filtrer par le mot partiel
            if (!string.IsNullOrEmpty(partialWord) && 
                !keyword.StartsWith(partialWord, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            completions.Add(new CompletionItem
            {
                Label = keyword,
                Kind = CompletionItemKind.Keyword,
                Detail = $"{keyword} operator",
                InsertText = keyword,
                FilterText = keyword
            });
        }

        // Variables (extraction simple depuis l'AST)
        if (document.Ast != null)
        {
            var variables = ExtractVariables(document.Ast);
            foreach (var variable in variables)
            {
                // Filtrer par le mot partiel
                if (!string.IsNullOrEmpty(partialWord) && 
                    !variable.StartsWith(partialWord, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                completions.Add(new CompletionItem
                {
                    Label = variable,
                    Kind = CompletionItemKind.Variable,
                    Detail = "Variable",
                    InsertText = variable,
                    FilterText = variable
                });
            }

            // Environnements
            var environments = ExtractEnvironments(document.Ast);
            foreach (var env in environments)
            {
                // Filtrer par le mot partiel
                if (!string.IsNullOrEmpty(partialWord) && 
                    !env.StartsWith(partialWord, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                completions.Add(new CompletionItem
                {
                    Label = env,
                    Kind = CompletionItemKind.EnumMember,
                    Detail = "Environment",
                    InsertText = env,
                    FilterText = env
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

    /// <summary>
    /// Extrait le texte du document jusqu'à la position du curseur.
    /// </summary>
    private string GetTextBeforeCursor(SettexDocument document, Position position)
    {
        var lines = document.Text.Split('\n');
        if (position.Line >= lines.Length)
        {
            return document.Text;
        }

        var textBeforeCursor = string.Join("\n", lines.Take((int)position.Line));
        var currentLine = lines[position.Line];
        var currentLinePrefix = currentLine.Substring(0, (int)System.Math.Min(position.Character, currentLine.Length));
        
        return textBeforeCursor + "\n" + currentLinePrefix;
    }

    /// <summary>
    /// Extrait le chemin d'objet avant le curseur (ex: "Server", "Logging.LogLevel").
    /// Retourne null si on n'est pas dans un contexte de propriété d'objet.
    /// </summary>
    private string? ExtractObjectPath(string textBeforeCursor)
    {
        // On cherche un pattern comme "Identifier.Identifier." à la fin du texte
        // Regex: identifier suivi de points, se terminant par un point
        var lines = textBeforeCursor.Split('\n');
        var lastLine = lines[^1].TrimStart();

        // Pattern: Word1.Word2.Word3. (se termine par un point)
        if (!lastLine.EndsWith('.'))
        {
            return null;
        }

        // Extraire tout ce qui précède le dernier point
        var pathWithDot = lastLine.TrimEnd();
        if (pathWithDot.Length == 0 || pathWithDot[^1] != '.')
        {
            return null;
        }

        // Retirer le point final
        var path = pathWithDot[..^1];

        // Vérifier que c'est bien un path valide (lettres, chiffres, underscores, points)
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Extraire seulement le dernier "mot.mot.mot" avant le curseur
        // Par ex: "Server.Port = 5000\n  Server.Host = " → on veut "Server.Host"
        var tokens = path.Split(new[] { ' ', '\t', '=', ',', '[', ']', '{', '}', '(', ')', '"' }, 
            System.StringSplitOptions.RemoveEmptyEntries);
        
        if (tokens.Length == 0)
        {
            return null;
        }

        var lastToken = tokens[^1];
        
        // Vérifier que c'est un identifiant valide avec possiblement des points
        if (!System.Text.RegularExpressions.Regex.IsMatch(lastToken, @"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$"))
        {
            return null;
        }

        this.logger.LogInformation("[COMPLETION] Extracted object path: {Path}", lastToken);
        return lastToken;
    }

    /// <summary>
    /// Extrait le mot partiel en cours de frappe avant le curseur (pour le filtrage).
    /// Par exemple: "Server.Po" → retourne "Po", "Ser" → retourne "Ser"
    /// </summary>
    private string? ExtractPartialWord(string textBeforeCursor)
    {
        var lines = textBeforeCursor.Split('\n');
        var lastLine = lines[^1];

        // Trouver le dernier mot (identifiant) avant le curseur
        // On cherche les caractères valides pour un identifiant depuis la fin
        int i = lastLine.Length - 1;
        while (i >= 0 && (char.IsLetterOrDigit(lastLine[i]) || lastLine[i] == '_'))
        {
            i--;
        }

        if (i == lastLine.Length - 1)
        {
            // Pas de mot partiel (curseur après un espace, point, etc.)
            return null;
        }

        var partialWord = lastLine.Substring(i + 1);
        return string.IsNullOrWhiteSpace(partialWord) ? null : partialWord;
    }

    /// <summary>
    /// Récupère toutes les propriétés d'un objet donné depuis base + tous les environnements.
    /// Retourne un dictionnaire : PropertyName → Liste des environnements où elle existe.
    /// </summary>
    private Dictionary<string, List<string>> GetObjectProperties(SettexDocument document, string path)
    {
        var properties = new Dictionary<string, List<string>>();

        if (document.Ast == null)
        {
            return properties;
        }

        try
        {
            // Évaluer les settings base + overlays
            var evaluation = this.EvaluateAllSettings(document.Ast);
            if (evaluation == null)
            {
                this.logger.LogWarning("[COMPLETION] EvaluateAllSettings returned null");
                return properties;
            }

            var (baseSettings, envOverlays) = evaluation.Value;

            // Naviguer dans l'objet base pour trouver les propriétés
            if (baseSettings != null)
            {
                var baseObject = this.NavigateToObject(baseSettings, path);
                if (baseObject != null)
                {
                    this.CollectProperties(baseObject, "Base", properties);
                }
            }

            // Naviguer dans chaque overlay
            var merger = new Merger();
            foreach (var (envName, overlay) in envOverlays)
            {
                // Merger base + overlay pour avoir l'état final
                var mergedSettings = baseSettings != null 
                    ? merger.Merge(baseSettings, overlay)
                    : overlay;
                
                var envObject = this.NavigateToObject(mergedSettings, path);
                
                if (envObject != null)
                {
                    this.CollectProperties(envObject, envName, properties);
                }
            }
        }
        catch (System.Exception ex)
        {
            this.logger.LogWarning(ex, "[COMPLETION] Error getting properties for path {Path}", path);
        }

        return properties;
    }

    /// <summary>
    /// Évalue tous les settings : base + overlays pour chaque environnement.
    /// </summary>
    private (JsonObject? BaseSettings, Dictionary<string, JsonObject> EnvOverlays)? EvaluateAllSettings(FileNode ast)
    {
        try
        {
            var evaluator = new Evaluator();
            var model = evaluator.Evaluate(ast);
            return (model.BaseSettings, model.EnvironmentOverlays);
        }
        catch (System.Exception ex)
        {
            this.logger.LogWarning(ex, "[COMPLETION] Evaluation failed");
            return null;
        }
    }

    /// <summary>
    /// Navigate dans un objet JSON en suivant un chemin avec points (ex: "Server.Connection").
    /// </summary>
    private JsonObject? NavigateToObject(JsonObject root, string path)
    {
        this.logger.LogInformation("[COMPLETION-NAV] Navigating to path '{Path}'", path);
        
        var segments = path.Split('.');
        JsonObject? current = root;

        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            this.logger.LogDebug("[COMPLETION-NAV] Looking for segment '{Segment}' (step {Step}/{Total})", 
                segment, i + 1, segments.Length);
            
            if (current == null)
            {
                this.logger.LogWarning("[COMPLETION-NAV] Current is null at segment '{Segment}'", segment);
                return null;
            }

            if (current.TryGetPropertyValue(segment, out var value))
            {
                this.logger.LogDebug("[COMPLETION-NAV] Found property '{Segment}', type: {Type}", 
                    segment, value?.GetType().Name ?? "null");
                
                if (value is JsonObject obj)
                {
                    current = obj;
                    this.logger.LogDebug("[COMPLETION-NAV] Navigated to object '{Segment}', properties: {Properties}", 
                        segment, string.Join(", ", obj.Select(p => p.Key)));
                }
                else
                {
                    this.logger.LogWarning("[COMPLETION-NAV] Property '{Segment}' is not an object (type: {Type})", 
                        segment, value?.GetType().Name ?? "null");
                    return null;
                }
            }
            else
            {
                this.logger.LogWarning("[COMPLETION-NAV] Property '{Segment}' not found. Available: {Available}", 
                    segment, string.Join(", ", current.Select(p => p.Key)));
                return null;
            }
        }

        this.logger.LogInformation("[COMPLETION-NAV] Successfully navigated to '{Path}', final object has {Count} properties", 
            path, current?.Count ?? 0);
        return current;
    }

    /// <summary>
    /// Collecte toutes les propriétés d'un objet JSON et les ajoute au dictionnaire.
    /// </summary>
    private void CollectProperties(JsonObject obj, string environmentName, Dictionary<string, List<string>> properties)
    {
        this.logger.LogInformation("[COMPLETION-COLLECT] Collecting properties from env '{Env}', object has {Count} properties", 
            environmentName, obj.Count);
        
        foreach (var property in obj)
        {
            var propertyName = property.Key;
            this.logger.LogDebug("[COMPLETION-COLLECT] Found property '{Name}' (type: {Type}) in env '{Env}'", 
                propertyName, property.Value?.GetType().Name ?? "null", environmentName);
            
            if (!properties.ContainsKey(propertyName))
            {
                properties[propertyName] = new List<string>();
            }

            if (!properties[propertyName].Contains(environmentName))
            {
                properties[propertyName].Add(environmentName);
            }
        }
        
        this.logger.LogInformation("[COMPLETION-COLLECT] Total unique properties collected: {Count}", properties.Count);
    }

    /// <summary>
    /// Vérifie si on est dans un contexte settings{} ou env{} où on peut définir des objets.
    /// </summary>
    private bool IsInSettingsOrEnvBlock(string textBeforeCursor)
    {
        // Simple heuristique : on cherche si on a "settings {" ou "env X {" avant le curseur
        // et qu'on n'est pas en train d'écrire une propriété (pas de point juste avant)
        
        var lines = textBeforeCursor.Split('\n');
        var lastLine = lines[^1].TrimStart();
        
        this.logger.LogInformation("[COMPLETION-CONTEXT] Checking if in settings/env block. Last line: '{Line}'", lastLine);
        
        // Si la dernière ligne se termine par un point, on est dans une propriété
        if (lastLine.TrimEnd().EndsWith('.'))
        {
            this.logger.LogInformation("[COMPLETION-CONTEXT] Last line ends with '.', not in settings/env block");
            return false;
        }
        
        // Si on est en train de taper après un identifiant suivi d'un point, ce n'est pas un nouveau nom d'objet
        var trimmedLine = lastLine.TrimEnd();
        if (trimmedLine.Contains('.'))
        {
            this.logger.LogInformation("[COMPLETION-CONTEXT] Line contains '.', likely in property path");
            return false;
        }
        
        // Chercher si on a un "settings {" ou "env X {" ouvert
        int braceDepth = 0;
        bool inSettingsOrEnv = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Vérifier si on entre dans un bloc settings ou env
            if (trimmed.StartsWith("settings") && trimmed.Contains('{'))
            {
                inSettingsOrEnv = true;
                this.logger.LogDebug("[COMPLETION-CONTEXT] Found 'settings {{' block");
            }
            else if (trimmed.StartsWith("env ") && trimmed.Contains('{'))
            {
                inSettingsOrEnv = true;
                this.logger.LogDebug("[COMPLETION-CONTEXT] Found 'env {{' block");
            }
            
            // Compter les accolades pour suivre la profondeur
            braceDepth += trimmed.Count(c => c == '{');
            braceDepth -= trimmed.Count(c => c == '}');
        }
        
        var result = inSettingsOrEnv && braceDepth > 0;
        this.logger.LogInformation("[COMPLETION-CONTEXT] In settings/env block: {Result} (inBlock={InBlock}, depth={Depth})", 
            result, inSettingsOrEnv, braceDepth);
        
        return result;
    }

    /// <summary>
    /// Récupère tous les noms d'objets de premier niveau définis dans base + tous les environnements.
    /// </summary>
    private Dictionary<string, List<string>> GetAllObjectNames(SettexDocument document)
    {
        var objectNames = new Dictionary<string, List<string>>();
        
        if (document.Ast == null)
        {
            return objectNames;
        }
        
        try
        {
            var evaluation = this.EvaluateAllSettings(document.Ast);
            if (evaluation == null)
            {
                this.logger.LogWarning("[COMPLETION] EvaluateAllSettings returned null");
                return objectNames;
            }
            
            var (baseSettings, envOverlays) = evaluation.Value;
            
            // Collecter les objets de base
            if (baseSettings != null)
            {
                this.CollectObjectNames(baseSettings, "Base", objectNames);
            }
            
            // Collecter les objets de chaque environnement (merged avec base)
            var merger = new Merger();
            foreach (var (envName, overlay) in envOverlays)
            {
                var mergedSettings = baseSettings != null 
                    ? merger.Merge(baseSettings, overlay)
                    : overlay;
                
                this.CollectObjectNames(mergedSettings, envName, objectNames);
            }
            
            this.logger.LogInformation("[COMPLETION] Found {Count} object names", objectNames.Count);
        }
        catch (System.Exception ex)
        {
            this.logger.LogWarning(ex, "[COMPLETION] Error getting object names");
        }
        
        return objectNames;
    }

    /// <summary>
    /// Collecte les noms d'objets de premier niveau depuis un JsonObject.
    /// </summary>
    private void CollectObjectNames(JsonObject settings, string environmentName, Dictionary<string, List<string>> objectNames)
    {
        foreach (var property in settings)
        {
            var propertyName = property.Key;
            
            // On ne garde que les propriétés qui sont des objets (pas des valeurs primitives)
            if (property.Value is JsonObject)
            {
                if (!objectNames.ContainsKey(propertyName))
                {
                    objectNames[propertyName] = new List<string>();
                }
                
                if (!objectNames[propertyName].Contains(environmentName))
                {
                    objectNames[propertyName].Add(environmentName);
                }
            }
        }
    }
}
