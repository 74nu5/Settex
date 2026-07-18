namespace Settex.LanguageServer;

using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Settex.Core.Evaluation;
using Settex.Core.Merging;
using Settex.Core.Parser.Ast;

/// <summary>
/// Formats hover content for assignment paths with overlay tracking.
/// Shows values across all environments with current environment highlighted.
/// </summary>
public static class HoverOverlayFormatter
{
    /// <summary>
    /// Formats assignment with overlay tracking showing base and all environment values.
    /// </summary>
    /// <param name="ast">The parsed AST.</param>
    /// <param name="path">The dotted path (e.g., "Server.Port").</param>
    /// <param name="currentEnvName">The current environment name (null if in base settings).</param>
    /// <param name="logger">Logger for debugging.</param>
    /// <returns>Formatted markdown string or null if path not found.</returns>
    public static string? FormatAssignmentWithOverlay(FileNode ast, string path, string? currentEnvName, ILogger? logger = null)
    {
        logger?.LogTrace("[FORMATTER] Starting format for path='{Path}', currentEnv='{CurrentEnv}'", path, currentEnvName ?? "(base)");
        
        // Evaluate all settings
        var evaluation = EvaluateAllSettings(ast, logger);
        if (evaluation == null)
        {
            logger?.LogTrace("[FORMATTER] EvaluateAllSettings returned null");
            return null;
        }

        var (baseSettings, environmentOverlays) = evaluation.Value;
        logger?.LogTrace("[FORMATTER] Evaluation successful. BaseSettings={HasBase}, Overlays count={Count}", 
            baseSettings != null ? "present" : "null", 
            environmentOverlays.Count);

        // Get base value
        var baseValue = GetValueAtPath(baseSettings, path, logger);
        logger?.LogTrace("[FORMATTER] Base value for '{Path}': {HasValue}", path, baseValue != null ? "FOUND" : "NULL");

        // Build result
        var result = new StringBuilder();
        result.AppendLine($"**Setting:** `{path}`");
        result.AppendLine();

        // If we're in base block (no env), just show base value
        if (currentEnvName == null)
        {
            logger?.LogTrace("[FORMATTER] In base block, returning base value only");
            if (baseValue == null)
            {
                logger?.LogTrace("[FORMATTER] Base value is null, returning null");
                return null;
            }

            result.AppendLine("**Value:**");
            result.AppendLine("```settex");
            result.AppendLine(FormatJsonValue(baseValue));
            result.AppendLine("```");
            var baseResult = result.ToString();
            logger?.LogTrace("[FORMATTER] Base result:\n{Result}", baseResult);
            return baseResult;
        }

        logger?.LogTrace("[FORMATTER] In environment block, showing all environments");
        // We're in an environment - show base + all envs
        result.AppendLine("| Environment | Value |");
        result.AppendLine("|-------------|-------|");

        // Base row
        var baseFormatted = baseValue != null ? FormatJsonValue(baseValue) : "*(not set)*";
        result.AppendLine($"| Base | `{baseFormatted}` |");

        // Merge base with each overlay to get final values
        var merger = new Merger();
        logger?.LogTrace("[FORMATTER] Processing {Count} environment overlays", environmentOverlays.Count);

        foreach (var kvp in environmentOverlays.OrderBy(e => e.Key))
        {
            var envName = kvp.Key;
            var overlay = kvp.Value;
            logger?.LogTrace("[FORMATTER] Processing overlay for env='{EnvName}'", envName);

            // Merge base + overlay to get final value for this environment
            JsonObject mergedSettings;
            if (baseSettings != null)
            {
                logger?.LogTrace("[FORMATTER] Merging base with overlay for env='{EnvName}'", envName);
                mergedSettings = merger.Merge(baseSettings, overlay);
            }
            else
            {
                logger?.LogTrace("[FORMATTER] No base settings, using overlay only for env='{EnvName}'", envName);
                mergedSettings = overlay;
            }

            var envValue = GetValueAtPath(mergedSettings, path, logger);
            var envFormatted = envValue != null ? FormatJsonValue(envValue) : "*(not set)*";
            logger?.LogTrace("[FORMATTER] Env '{EnvName}' value for '{Path}': {Formatted}", envName, path, envFormatted);

            // Highlight current environment
            if (envName == currentEnvName)
            {
                logger?.LogTrace("[FORMATTER] Highlighting current environment: {EnvName}", envName);
                result.AppendLine($"| **{envName}** ✓ | `{envFormatted}` |");
            }
            else
            {
                result.AppendLine($"| {envName} | `{envFormatted}` |");
            }
        }

        var finalResult = result.ToString();
        logger?.LogTrace("[FORMATTER] Final result:\n{Result}", finalResult);
        return finalResult;
    }

    /// <summary>
    /// Formats an object with all its properties showing values across all environments.
    /// </summary>
    /// <param name="ast">The parsed AST.</param>
    /// <param name="objectPath">The dotted path to the object (e.g., "Server").</param>
    /// <param name="currentEnvName">The current environment name (null if in base settings).</param>
    /// <param name="logger">Logger for debugging.</param>
    /// <returns>Formatted markdown string or null if object not found.</returns>
    public static string? FormatObjectWithOverlay(FileNode ast, string objectPath, string? currentEnvName, ILogger? logger = null)
    {
        logger?.LogTrace("[FORMATTER-OBJ] Starting format for object path='{Path}', currentEnv='{CurrentEnv}'", objectPath, currentEnvName ?? "(base)");
        
        // Evaluate all settings
        var evaluation = EvaluateAllSettings(ast, logger);
        if (evaluation == null)
        {
            logger?.LogTrace("[FORMATTER-OBJ] EvaluateAllSettings returned null");
            return null;
        }

        var (baseSettings, environmentOverlays) = evaluation.Value;
        logger?.LogTrace("[FORMATTER-OBJ] Evaluation successful. BaseSettings={HasBase}, Overlays count={Count}", 
            baseSettings != null ? "present" : "null", 
            environmentOverlays.Count);

        // Get base object (may be null if only defined in environments)
        var baseObject = GetValueAtPath(baseSettings, objectPath, logger);
        logger?.LogTrace("[FORMATTER-OBJ] Base object for '{Path}': {HasValue}, type={Type}", 
            objectPath, 
            baseObject != null ? "FOUND" : "NULL",
            baseObject?.GetType().Name ?? "null");

        JsonObject? baseJsonObject = null;
        if (baseObject is JsonObject jsonObj)
        {
            baseJsonObject = jsonObj;
        }

        // Collect all unique property names from base and all environments
        var allPropertyNames = new HashSet<string>();
        
        // Add properties from base object (if exists)
        if (baseJsonObject != null)
        {
            foreach (var prop in baseJsonObject)
            {
                allPropertyNames.Add(prop.Key);
            }
        }

        // Merge base with each overlay and collect properties
        var merger = new Merger();
        var environmentObjects = new Dictionary<string, JsonObject>();

        foreach (var kvp in environmentOverlays)
        {
            var envName = kvp.Key;
            var overlay = kvp.Value;

            JsonObject mergedSettings;
            if (baseSettings != null)
            {
                mergedSettings = merger.Merge(baseSettings, overlay);
            }
            else
            {
                mergedSettings = overlay;
            }

            var envObject = GetValueAtPath(mergedSettings, objectPath, logger);
            if (envObject is JsonObject envJsonObject)
            {
                environmentObjects[envName] = envJsonObject;
                foreach (var prop in envJsonObject)
                {
                    allPropertyNames.Add(prop.Key);
                }
            }
        }

        // If no properties found anywhere, the object doesn't exist
        if (allPropertyNames.Count == 0)
        {
            logger?.LogTrace("[FORMATTER-OBJ] Object '{Path}' has no properties in any environment", objectPath);
            return null;
        }

        // Build result
        var result = new StringBuilder();
        result.AppendLine($"**Object:** `{objectPath}`");
        result.AppendLine();

        // If we're in base block (no env), just show base properties
        if (currentEnvName == null)
        {
            logger?.LogTrace("[FORMATTER-OBJ] In base block, showing base properties only");
            
            // If object doesn't exist in base, show message
            if (baseJsonObject == null)
            {
                result.AppendLine("*This object is not defined in base settings. It only exists in specific environments.*");
                result.AppendLine();
                result.AppendLine("**Defined in environments:**");
                foreach (var envName in environmentObjects.Keys.OrderBy(e => e))
                {
                    result.AppendLine($"- {envName}");
                }
                var baseResult = result.ToString();
                logger?.LogTrace("[FORMATTER-OBJ] Base result (no base object):\n{Result}", baseResult);
                return baseResult;
            }
            
            result.AppendLine("**Properties:**");
            result.AppendLine();
            result.AppendLine("| Property | Value |");
            result.AppendLine("|----------|-------|");

            foreach (var propName in allPropertyNames.OrderBy(p => p))
            {
                if (baseJsonObject.TryGetPropertyValue(propName, out var value) && value != null)
                {
                    var formatted = FormatJsonValue(value);
                    result.AppendLine($"| {propName} | `{formatted}` |");
                }
                else
                {
                    result.AppendLine($"| {propName} | `*(not set)*` |");
                }
            }

            var baseResult2 = result.ToString();
            logger?.LogTrace("[FORMATTER-OBJ] Base result:\n{Result}", baseResult2);
            return baseResult2;
        }

        // We're in an environment - show properties across all environments
        logger?.LogTrace("[FORMATTER-OBJ] In environment block, showing all environments");
        
        // Create header with environment names
        result.Append("| Property | Base |");
        foreach (var envName in environmentOverlays.Keys.OrderBy(e => e))
        {
            if (envName == currentEnvName)
            {
                result.Append($" **{envName}** ✓ |");
            }
            else
            {
                result.Append($" {envName} |");
            }
        }
        result.AppendLine();

        // Create separator
        result.Append("|----------|------|");
        for (int i = 0; i < environmentOverlays.Count; i++)
        {
            result.Append("------|");
        }
        result.AppendLine();

        // Add rows for each property
        foreach (var propName in allPropertyNames.OrderBy(p => p))
        {
            result.Append($"| **{propName}** |");

            // Base value
            if (baseJsonObject != null && baseJsonObject.TryGetPropertyValue(propName, out var baseValue) && baseValue != null)
            {
                var formatted = FormatJsonValue(baseValue);
                result.Append($" `{formatted}` |");
            }
            else
            {
                result.Append(" `*(not set)*` |");
            }

            // Environment values
            foreach (var envName in environmentOverlays.Keys.OrderBy(e => e))
            {
                if (environmentObjects.TryGetValue(envName, out var envObj) && 
                    envObj.TryGetPropertyValue(propName, out var envValue) &&
                    envValue != null)
                {
                    var formatted = FormatJsonValue(envValue);
                    result.Append($" `{formatted}` |");
                }
                else
                {
                    result.Append(" `*(not set)*` |");
                }
            }
            result.AppendLine();
        }

        var finalResult = result.ToString();
        logger?.LogTrace("[FORMATTER-OBJ] Final object result:\n{Result}", finalResult);
        return finalResult;
    }

    /// <summary>
    /// Evaluates all settings from an AST.
    /// </summary>
    private static (JsonObject? BaseSettings, Dictionary<string, JsonObject> EnvironmentOverlays)? EvaluateAllSettings(FileNode ast, ILogger? logger)
    {
        logger?.LogTrace("[FORMATTER-EVAL] Starting evaluation of AST");
        try
        {
            var evaluator = new Evaluator();
            var model = evaluator.Evaluate(ast);
            logger?.LogTrace("[FORMATTER-EVAL] Evaluation successful. BaseSettings={HasBase}, Overlays={Count}",
                model.BaseSettings != null ? "present" : "null",
                model.EnvironmentOverlays.Count);

            return (model.BaseSettings, model.EnvironmentOverlays);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[FORMATTER-EVAL] Evaluation failed");
            return null;
        }
    }

    /// <summary>
    /// Gets a value at a dotted path in a JsonObject.
    /// </summary>
    private static JsonNode? GetValueAtPath(JsonObject? obj, string path, ILogger? logger = null)
    {
        logger?.LogTrace("[FORMATTER-PATH] Getting value at path '{Path}' from {ObjState}", path, obj != null ? "JsonObject" : "null");
        
        if (obj == null || string.IsNullOrEmpty(path))
        {
            logger?.LogTrace("[FORMATTER-PATH] Returning null - obj is null or path is empty");
            return null;
        }

        var parts = path.Split('.');
        logger?.LogTrace("[FORMATTER-PATH] Path parts: {Parts}", string.Join(", ", parts));
        JsonNode? current = obj;

        foreach (var part in parts)
        {
            if (current is JsonObject objectNode)
            {
                if (objectNode.TryGetPropertyValue(part, out var value))
                {
                    logger?.LogTrace("[FORMATTER-PATH] Found property '{Part}', type={Type}", part, value?.GetType().Name ?? "null");
                    current = value;
                }
                else
                {
                    logger?.LogTrace("[FORMATTER-PATH] Property '{Part}' not found in object - available keys: {Keys}", 
                        part, 
                        string.Join(", ", objectNode.Select(p => p.Key)));
                    return null;
                }
            }
            else
            {
                logger?.LogTrace("[FORMATTER-PATH] Current node is not an object (type={Type}), cannot navigate to '{Part}'", 
                    current?.GetType().Name ?? "null", 
                    part);
                return null;
            }
        }

        logger?.LogTrace("[FORMATTER-PATH] Final value found: {Value}", current?.ToJsonString() ?? "null");
        return current;
    }

    /// <summary>
    /// Formats a JSON value for display in hover.
    /// </summary>
    private static string FormatJsonValue(JsonNode node)
    {
        // For simple values, use compact format
        var options = new JsonSerializerOptions { WriteIndented = false };
        return node.ToJsonString(options);
    }
}
