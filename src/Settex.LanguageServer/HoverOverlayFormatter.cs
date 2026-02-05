namespace Settex.LanguageServer;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    /// <returns>Formatted markdown string or null if path not found.</returns>
    public static string? FormatAssignmentWithOverlay(FileNode ast, string path, string? currentEnvName)
    {
        // Evaluate all settings
        var evaluation = EvaluateAllSettings(ast);
        if (evaluation == null)
        {
            return null;
        }

        var (baseSettings, environmentOverlays) = evaluation.Value;

        // Get base value
        var baseValue = GetValueAtPath(baseSettings, path);

        // Build result
        var result = new StringBuilder();
        result.AppendLine($"**Setting:** `{path}`");
        result.AppendLine();

        // If we're in base block (no env), just show base value
        if (currentEnvName == null)
        {
            if (baseValue == null)
            {
                return null;
            }

            result.AppendLine("**Value:**");
            result.AppendLine("```settex");
            result.AppendLine(FormatJsonValue(baseValue));
            result.AppendLine("```");
            return result.ToString();
        }

        // We're in an environment - show base + all envs
        result.AppendLine("| Environment | Value |");
        result.AppendLine("|-------------|-------|");

        // Base row
        var baseFormatted = baseValue != null ? FormatJsonValue(baseValue) : "*(not set)*";
        result.AppendLine($"| Base | `{baseFormatted}` |");

        // Merge base with each overlay to get final values
        var merger = new Merger();

        foreach (var kvp in environmentOverlays.OrderBy(e => e.Key))
        {
            var envName = kvp.Key;
            var overlay = kvp.Value;

            // Merge base + overlay to get final value for this environment
            JsonObject mergedSettings;
            if (baseSettings != null)
            {
                mergedSettings = merger.Merge(baseSettings, overlay);
            }
            else
            {
                mergedSettings = overlay;
            }

            var envValue = GetValueAtPath(mergedSettings, path);
            var envFormatted = envValue != null ? FormatJsonValue(envValue) : "*(not set)*";

            // Highlight current environment
            if (envName == currentEnvName)
            {
                result.AppendLine($"| **{envName}** ✓ | `{envFormatted}` |");
            }
            else
            {
                result.AppendLine($"| {envName} | `{envFormatted}` |");
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Evaluates all settings from an AST.
    /// </summary>
    private static (JsonObject? BaseSettings, Dictionary<string, JsonObject> EnvironmentOverlays)? EvaluateAllSettings(FileNode ast)
    {
        try
        {
            var evaluator = new Evaluator();
            var model = evaluator.Evaluate(ast);
            return (model.BaseSettings, model.EnvironmentOverlays);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a value at a dotted path in a JsonObject.
    /// </summary>
    private static JsonNode? GetValueAtPath(JsonObject? obj, string path)
    {
        if (obj == null || string.IsNullOrEmpty(path))
        {
            return null;
        }

        var parts = path.Split('.');
        JsonNode? current = obj;

        foreach (var part in parts)
        {
            if (current is JsonObject objectNode)
            {
                if (objectNode.TryGetPropertyValue(part, out var value))
                {
                    current = value;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

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
