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

    /// <summary>
    /// Degrades to an empty completion list instead of faulting the request.
    /// </summary>
    public override async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        try
        {
            return await this.HandleCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.Exception ex)
        {
            this.logger.LogError(ex, "Completion failed for {Uri}", request.TextDocument.Uri);
            return new CompletionList();
        }
    }

    private Task<CompletionList> HandleCoreAsync(CompletionParams request, CancellationToken cancellationToken)
    {
        // Makes the OperationCanceledException arm of the caller's guard real
        // instead of dead code: a request the client already withdrew does no work.
        cancellationToken.ThrowIfCancellationRequested();

        var uri = request.TextDocument.Uri.ToString();
        var document = this.workspace.GetDocument(uri);

        if (document == null)
        {
            return Task.FromResult(new CompletionList());
        }

        // Capture a single immutable snapshot so the cursor offset (from Text) and
        // the AST stay consistent even if the document is updated concurrently.
        var snapshot = document.Current;

        var completions = new List<CompletionItem>();

        // Détection du contexte : est-on après un point pour une propriété d'objet ?
        var textBeforeCursor = this.GetTextBeforeCursor(snapshot.Text, request.Position);
        var objectPath = this.ExtractObjectPath(textBeforeCursor);

        // Extraire le mot partiel en cours de frappe pour le filtrage
        var partialWord = this.ExtractPartialWord(textBeforeCursor);
        this.logger.LogTrace("[COMPLETION] Partial word: '{PartialWord}'", partialWord ?? "(none)");

        if (!string.IsNullOrEmpty(objectPath))
        {
            // Autocomplétion des propriétés d'objet
            this.logger.LogTrace("[COMPLETION] Object property completion for path: {Path}", objectPath);
            var properties = this.GetObjectProperties(snapshot, objectPath);
            
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
                this.logger.LogTrace("[COMPLETION] Returning {Count} property completions", completions.Count);
                return Task.FromResult(new CompletionList(completions));
            }
        }

        // Autocomplétion générale : keywords, variables, environments, et noms d'objets existants

        // Proposer les noms d'objets existants en priorité
        this.logger.LogTrace("[COMPLETION] Adding existing object names");
        var objectNames = this.GetAllObjectNames(snapshot);
        
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
        if (snapshot.Ast != null)
        {
            var variables = ExtractVariables(snapshot.Ast);
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
            var environments = ExtractEnvironments(snapshot.Ast);
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

    /// <summary>
    /// Resolve step. Nothing extra to compute, so the item is returned as-is —
    /// but honouring cancellation keeps it consistent with the other handlers.
    /// </summary>
    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
    private string GetTextBeforeCursor(string text, Position position)
    {
        var lines = text.Split('\n');
        if (position.Line >= lines.Length)
        {
            return text;
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
        var tokens = path.Split(new[] { ' ', '\t', '=', ',', '[', ']', '{', '}', '(', ')', '"', ';' }, 
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

        this.logger.LogTrace("[COMPLETION] Extracted object path: {Path}", lastToken);
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
    private Dictionary<string, List<string>> GetObjectProperties(SettexDocument.Snapshot snapshot, string path)
    {
        var properties = new Dictionary<string, List<string>>();

        if (snapshot.Ast == null)
        {
            return properties;
        }

        try
        {
            // Évaluer les settings base + overlays
            var evaluation = this.EvaluateAllSettings(snapshot.Ast);
            if (evaluation == null)
            {
                this.logger.LogTrace("[COMPLETION] EvaluateAllSettings returned null");
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
            // Diagnostics verbeux : ne calculer les comptages/joins que si le niveau
            // Trace est actif (sinon ces OfType/Count/Join tournent à chaque requête
            // de complétion pour rien).
            if (this.logger.IsEnabled(LogLevel.Trace))
            {
                this.logger.LogTrace("[COMPLETION-EVAL] Evaluating AST with {Count} statements", ast.Statements.Count);
                var settingsBlocks = ast.Statements.OfType<SettingsBlockNode>().Count();
                var envBlocks = ast.Statements.OfType<EnvBlockNode>().Count();
                var letStatements = ast.Statements.OfType<LetNode>().Count();
                this.logger.LogTrace("[COMPLETION-EVAL] AST contains: {Settings} settings blocks, {Env} env blocks, {Let} let statements",
                    settingsBlocks, envBlocks, letStatements);

                var settingsBlock = ast.Statements.OfType<SettingsBlockNode>().FirstOrDefault();
                if (settingsBlock != null)
                {
                    var assignments = settingsBlock.Block.Statements.OfType<AssignmentNode>().ToList();
                    this.logger.LogTrace("[COMPLETION-EVAL] Settings block has {Count} assignments", assignments.Count);

                    foreach (var assignment in assignments)
                    {
                        var path = string.Join(".", assignment.Path.Segments);
                        this.logger.LogTrace("[COMPLETION-EVAL] Assignment: {Path}", path);
                    }
                }
            }

            var evaluator = new Evaluator();
            var model = evaluator.Evaluate(ast, requireSettingsBlock: false);

            if (this.logger.IsEnabled(LogLevel.Trace))
            {
                this.logger.LogTrace("[COMPLETION-EVAL] Evaluation complete. Base has {BaseCount} properties",
                    model.BaseSettings?.Count ?? 0);
                if (model.BaseSettings != null)
                {
                    var keys = string.Join(", ", model.BaseSettings.Select(p => p.Key));
                    this.logger.LogTrace("[COMPLETION-EVAL] Base properties: {Keys}", keys);
                }
            }

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
        this.logger.LogTrace("[COMPLETION-NAV] Navigating to path '{Path}'", path);
        
        var segments = path.Split('.');
        JsonObject? current = root;

        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            this.logger.LogTrace("[COMPLETION-NAV] Looking for segment '{Segment}' (step {Step}/{Total})", 
                segment, i + 1, segments.Length);
            
            if (current == null)
            {
                this.logger.LogTrace("[COMPLETION-NAV] Current is null at segment '{Segment}'", segment);
                return null;
            }

            if (current.TryGetPropertyValue(segment, out var value))
            {
                this.logger.LogTrace("[COMPLETION-NAV] Found property '{Segment}', type: {Type}", 
                    segment, value?.GetType().Name ?? "null");
                
                if (value is JsonObject obj)
                {
                    current = obj;
                    if (this.logger.IsEnabled(LogLevel.Trace))
                    {
                        this.logger.LogTrace("[COMPLETION-NAV] Navigated to object '{Segment}', properties: {Properties}",
                            segment, string.Join(", ", obj.Select(p => p.Key)));
                    }
                }
                else
                {
                    this.logger.LogTrace("[COMPLETION-NAV] Property '{Segment}' is not an object (type: {Type})", 
                        segment, value?.GetType().Name ?? "null");
                    return null;
                }
            }
            else
            {
                if (this.logger.IsEnabled(LogLevel.Trace))
                {
                    this.logger.LogTrace("[COMPLETION-NAV] Property '{Segment}' not found. Available: {Available}",
                        segment, string.Join(", ", current.Select(p => p.Key)));
                }

                return null;
            }
        }

        this.logger.LogTrace("[COMPLETION-NAV] Successfully navigated to '{Path}', final object has {Count} properties", 
            path, current?.Count ?? 0);
        return current;
    }

    /// <summary>
    /// Collecte toutes les propriétés d'un objet JSON et les ajoute au dictionnaire.
    /// </summary>
    private void CollectProperties(JsonObject obj, string environmentName, Dictionary<string, List<string>> properties)
    {
        this.logger.LogTrace("[COMPLETION-COLLECT] Collecting properties from env '{Env}', object has {Count} properties", 
            environmentName, obj.Count);
        
        foreach (var property in obj)
        {
            var propertyName = property.Key;
            this.logger.LogTrace("[COMPLETION-COLLECT] Found property '{Name}' (type: {Type}) in env '{Env}'", 
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
        
        this.logger.LogTrace("[COMPLETION-COLLECT] Total unique properties collected: {Count}", properties.Count);
    }

    /// <summary>
    /// Récupère tous les noms d'objets de premier niveau définis dans base + tous les environnements.
    /// </summary>
    private Dictionary<string, List<string>> GetAllObjectNames(SettexDocument.Snapshot snapshot)
    {
        var objectNames = new Dictionary<string, List<string>>();
        
        if (snapshot.Ast == null)
        {
            return objectNames;
        }
        
        try
        {
            var evaluation = this.EvaluateAllSettings(snapshot.Ast);
            if (evaluation == null)
            {
                this.logger.LogTrace("[COMPLETION] EvaluateAllSettings returned null");
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
            
            this.logger.LogTrace("[COMPLETION] Found {Count} object names", objectNames.Count);
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
