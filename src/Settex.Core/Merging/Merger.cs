namespace Settex.Core.Merging;

using System.Text.Json.Nodes;

/// <summary>
///     Merges JSON objects according to Settex rules.
/// </summary>
public sealed class Merger
{
    /// <summary>
    ///     Merges overlay onto base using Settex merge rules.
    /// </summary>
    /// <param name="baseNode">Base JSON object.</param>
    /// <param name="overlay">Overlay JSON object to merge.</param>
    /// <returns>Merged JSON object.</returns>
    /// <exception cref="MergerException">Thrown when type mismatch occurs.</exception>
    public JsonObject Merge(JsonObject baseNode, JsonObject overlay)
    {
        var result = new JsonObject();

        // Copy all base properties
        foreach (var kvp in baseNode)
        {
            result[kvp.Key] = this.DeepClone(kvp.Value);
        }

        // Apply overlay properties
        foreach (var kvp in overlay)
        {
            var overlayValue = kvp.Value;

            // Match the base key case-insensitively, and keep the base's spelling.
            // Configuration keys are case-insensitive, so a base Server.Port and an
            // overlay server.port are one key — writing both into the merged file
            // produced JSON that .NET refuses to load outright ("A duplicate key
            // 'server:port' was found"), which means merged output claimed an effective
            // configuration that could not exist.
            var key = FindKeyIgnoreCase(result, kvp.Key) ?? kvp.Key;

            if (!result.ContainsKey(key))
            {
                // New key from overlay
                result[key] = this.DeepClone(overlayValue);
            }
            else
            {
                // Key exists in both base and overlay - merge
                var baseValue = result[key];
                result[key] = this.MergeValues(baseValue, overlayValue, key);
            }
        }

        return result;
    }

    /// <summary>
    ///     The key of <paramref name="source" /> that matches <paramref name="key" />
    ///     case-insensitively, or <c>null</c>. Returns the existing spelling so a merge
    ///     keeps one key rather than producing two that collide at runtime.
    /// </summary>
    public static string? FindKeyIgnoreCase(JsonObject source, string key)
    {
        foreach (var (candidate, _) in source)
        {
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    ///     Merges two JSON values according to Settex rules.
    /// </summary>
    private JsonNode? MergeValues(JsonNode? baseValue, JsonNode? overlayValue, string key)
    {
        // A null base commits to no type: whatever the overlay supplies wins.
        // See AreCompatible, which must stay in step with this.
        if (baseValue is null)
        {
            return this.DeepClone(overlayValue);
        }

        // If both are objects, merge recursively
        if (baseValue is JsonObject baseObj && overlayValue is JsonObject overlayObj)
        {
            return this.Merge(baseObj, overlayObj);
        }

        // If both are arrays, replace entire array (overlay wins)
        if (baseValue is JsonArray && overlayValue is JsonArray)
        {
            return this.DeepClone(overlayValue);
        }

        // If both are primitives (or null), replace (overlay wins)
        if (IsPrimitive(baseValue) && IsPrimitive(overlayValue))
        {
            return this.DeepClone(overlayValue);
        }

        // Type mismatch - error
        throw new MergerException(
            $"Type mismatch for key '{key}': base is {DescribeNodeType(baseValue)}, overlay is {DescribeNodeType(overlayValue)}");
    }

    /// <summary>
    ///     Whether a base value and an overlay value can be combined: both objects
    ///     (deep merge), both arrays (replace) or both primitives (replace). Anything
    ///     else is a type conflict. Shared with the compile-time validation so the
    ///     rules cannot drift from the merge itself.
    ///     <para>
    ///     A <c>null</c> <em>base</em> is compatible with anything. Null is the one
    ///     value that commits to no type — it declares that a key exists and is
    ///     deliberately unset — so "declare it in the base, fill it in per environment"
    ///     is a legitimate pattern rather than a conflict.
    ///     </para>
    ///     <para>
    ///     The reverse is <strong>not</strong> symmetric: a <c>null</c> overlay over a
    ///     structured base stays a conflict. It reads as "unset this here", and .NET
    ///     cannot do that — the base's keys survive the null and the intent fails
    ///     silently, which is exactly the kind of surprise this check exists to catch.
    ///     </para>
    /// </summary>
    public static bool AreCompatible(JsonNode? baseValue, JsonNode? overlayValue)
        => baseValue is null
           || (baseValue is JsonObject && overlayValue is JsonObject)
           || (baseValue is JsonArray && overlayValue is JsonArray)
           || (IsPrimitive(baseValue) && IsPrimitive(overlayValue));

    /// <summary>
    ///     Gets a human-readable type name for a JSON node.
    /// </summary>
    public static string DescribeNodeType(JsonNode? node)
    {
        return node switch
        {
            null => "null",
            JsonObject => "object",
            JsonArray => "array",
            JsonValue => "primitive",
            _ => "unknown",
        };
    }

    /// <summary>
    ///     Checks if a JSON node is a primitive value (string, number, boolean, null).
    /// </summary>
    private static bool IsPrimitive(JsonNode? node)
        => node is null or JsonValue;

    /// <summary>
    ///     Deep clones a JSON node.
    /// </summary>
    private JsonNode? DeepClone(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        // JsonNode.DeepClone() is available
        return node.DeepClone();
    }
}
