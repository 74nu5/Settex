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
            var key = kvp.Key;
            var overlayValue = kvp.Value;

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
    ///     Merges two JSON values according to Settex rules.
    /// </summary>
    private JsonNode? MergeValues(JsonNode? baseValue, JsonNode? overlayValue, string key)
    {
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
        if (this.IsPrimitive(baseValue) && this.IsPrimitive(overlayValue))
        {
            return this.DeepClone(overlayValue);
        }

        // Type mismatch - error
        var baseType = this.GetNodeTypeName(baseValue);
        var overlayType = this.GetNodeTypeName(overlayValue);
        throw new MergerException(
            $"Type mismatch for key '{key}': base is {baseType}, overlay is {overlayType}");
    }

    /// <summary>
    ///     Checks if a JSON node is a primitive value (string, number, boolean, null).
    /// </summary>
    private bool IsPrimitive(JsonNode? node)
        => node is null or JsonValue;

    /// <summary>
    ///     Gets a human-readable type name for a JSON node.
    /// </summary>
    private string GetNodeTypeName(JsonNode? node)
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
