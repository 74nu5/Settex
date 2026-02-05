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
        logger?.LogInformation("[FORMATTER] Starting format for path='{Path}', currentEnv='{CurrentEnv}'", path, currentEnvName ?? "(base)");
        
        // Evaluate all settings
        var evaluation = EvaluateAllSettings(ast, logger);
        if (evaluation == null)
        {
            logger?.LogWarning("[FORMATTER] EvaluateAllSettings returned null");
            return null;
        }

        var (baseSettings, environmentOverlays) = evaluation.Value;
        logger?.LogInformation("[FORMATTER] Evaluation successful. BaseSettings={HasBase}, Overlays count={Count}", 
            baseSettings != null ? "present" : "null", 
            environmentOverlays.Count);

        // Get base value
        var baseValue = GetValueAtPath(baseSettings, path, logger);
        logger?.LogInformation("[FORMATTER] Base value for '{Path}': {HasValue}", path, baseValue != null ? "FOUND" : "NULL");

        // Build result
        var result = new StringBuilder();
        result.AppendLine($"**Setting:** `{path}`");
        result.AppendLine();

        // If we're in base block (no env), just show base value
        if (currentEnvName == null)
        {
            logger?.LogInformation("[FORMATTER] In base block, returning base value only");
            if (baseValue == null)
            {
                logger?.LogWarning("[FORMATTER] Base value is null, returning null");
                return null;
            }

            result.AppendLine("**Value:**");
            result.AppendLine("```settex");
            result.AppendLine(FormatJsonValue(baseValue));
            result.AppendLine("```");
            var baseResult = result.ToString();
            logger?.LogDebug("[FORMATTER] Base result:\n{Result}", baseResult);
            return baseResult;
        }

        logger?.LogInformation("[FORMATTER] In environment block, showing all environments");
        // We're in an environment - show base + all envs
        result.AppendLine("| Environment | Value |");
        result.AppendLine("|-------------|-------|");

        // Base row
        var baseFormatted = baseValue != null ? FormatJsonValue(baseValue) : "*(not set)*";
        result.AppendLine($"| Base | `{baseFormatted}` |");

        // Merge base with each overlay to get final values
        var merger = new Merger();
        logger?.LogInformation("[FORMATTER] Processing {Count} environment overlays", environmentOverlays.Count);

        foreach (var kvp in environmentOverlays.OrderBy(e => e.Key))
        {
            var envName = kvp.Key;
            var overlay = kvp.Value;
            logger?.LogInformation("[FORMATTER] Processing overlay for env='{EnvName}'", envName);

            // Merge base + overlay to get final value for this environment
            JsonObject mergedSettings;
            if (baseSettings != null)
            {
                logger?.LogDebug("[FORMATTER] Merging base with overlay for env='{EnvName}'", envName);
                mergedSettings = merger.Merge(baseSettings, overlay);
            }
            else
            {
                logger?.LogDebug("[FORMATTER] No base settings, using overlay only for env='{EnvName}'", envName);
                mergedSettings = overlay;
            }

            var envValue = GetValueAtPath(mergedSettings, path, logger);
            var envFormatted = envValue != null ? FormatJsonValue(envValue) : "*(not set)*";
            logger?.LogInformation("[FORMATTER] Env '{EnvName}' value for '{Path}': {Formatted}", envName, path, envFormatted);

            // Highlight current environment
            if (envName == currentEnvName)
            {
                logger?.LogInformation("[FORMATTER] Highlighting current environment: {EnvName}", envName);
                result.AppendLine($"| **{envName}** ✓ | `{envFormatted}` |");
            }
            else
            {
                result.AppendLine($"| {envName} | `{envFormatted}` |");
            }
        }

        var finalResult = result.ToString();
        logger?.LogDebug("[FORMATTER] Final result:\n{Result}", finalResult);
        return finalResult;
    }

    /// <summary>
    /// Evaluates all settings from an AST.
    /// </summary>
    private static (JsonObject? BaseSettings, Dictionary<string, JsonObject> EnvironmentOverlays)? EvaluateAllSettings(FileNode ast, ILogger? logger)
    {
        logger?.LogInformation("[FORMATTER-EVAL] Starting evaluation of AST");
        try
        {
            var evaluator = new Evaluator();
            var model = evaluator.Evaluate(ast);
            logger?.LogInformation("[FORMATTER-EVAL] Evaluation successful. BaseSettings={HasBase}, Overlays={Count}", 
                model.BaseSettings != null ? "present" : "null",
                model.EnvironmentOverlays.Count);
            
            if (model.BaseSettings != null)
            {
                logger?.LogDebug("[FORMATTER-EVAL] Base settings JSON:\n{Json}", model.BaseSettings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            
            foreach (var kvp in model.EnvironmentOverlays)
            {
                logger?.LogDebug("[FORMATTER-EVAL] Overlay '{EnvName}' JSON:\n{Json}", kvp.Key, kvp.Value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            
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
        logger?.LogDebug("[FORMATTER-PATH] Getting value at path '{Path}' from {ObjState}", path, obj != null ? "JsonObject" : "null");
        
        if (obj == null || string.IsNullOrEmpty(path))
        {
            logger?.LogDebug("[FORMATTER-PATH] Returning null - obj is null or path is empty");
            return null;
        }

        var parts = path.Split('.');
        logger?.LogDebug("[FORMATTER-PATH] Path parts: {Parts}", string.Join(", ", parts));
        JsonNode? current = obj;

        foreach (var part in parts)
        {
            if (current is JsonObject objectNode)
            {
                if (objectNode.TryGetPropertyValue(part, out var value))
                {
                    logger?.LogDebug("[FORMATTER-PATH] Found property '{Part}', type={Type}", part, value?.GetType().Name ?? "null");
                    current = value;
                }
                else
                {
                    logger?.LogDebug("[FORMATTER-PATH] Property '{Part}' not found in object - available keys: {Keys}", 
                        part, 
                        string.Join(", ", objectNode.Select(p => p.Key)));
                    return null;
                }
            }
            else
            {
                logger?.LogDebug("[FORMATTER-PATH] Current node is not an object (type={Type}), cannot navigate to '{Part}'", 
                    current?.GetType().Name ?? "null", 
                    part);
                return null;
            }
        }

        logger?.LogDebug("[FORMATTER-PATH] Final value found: {Value}", current?.ToJsonString() ?? "null");
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
