namespace Settex.Compilation;

using System.Text.Json.Nodes;

using Settex.Core.Evaluation;

/// <summary>
///     Warns about the .NET array-layering trap. .NET's configuration provider does not
///     replace an array when a later file redefines it: it flattens every file into one
///     key/value dictionary (<c>Svcs:0:Port</c>) and layers the entries. Two distinct
///     leaks follow, and an environment override that looks like a replacement is
///     neither:
///     <list type="bullet">
///       <item>
///         <strong>Shorter array.</strong> The override sets indices 0..n-1 and the
///         base's remaining indices survive.
///       </item>
///       <item>
///         <strong>Object elements.</strong> At a shared index, two objects merge
///         <em>field by field</em>, so any field the base element defines and the
///         override omits survives — no matter how long either array is.
///       </item>
///     </list>
///     Settex cannot change how the provider layers arrays, so it surfaces the hazard at
///     compile time instead of letting it fail silently.
/// </summary>
public static class ArrayLayeringAnalyzer
{
    /// <summary>
    ///     Returns a warning for every environment array override that would leak
    ///     base content at runtime. Advisory only — never an error.
    /// </summary>
    public static IReadOnlyList<Diagnostic> Analyze(SettingsModel model)
    {
        var diagnostics = new List<Diagnostic>();
        var baseArrays = CollectArrays(model.BaseSettings);

        // Deterministic order: EnvironmentOverlays is a Dictionary, whose enumeration
        // order is an implementation detail.
        foreach (var envName in model.EnvironmentOverlays.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            var overlayArrays = CollectArrays(model.EnvironmentOverlays[envName]);

            foreach (var path in overlayArrays.Keys.OrderBy(p => p, StringComparer.Ordinal))
            {
                var overlayArray = overlayArrays[path];

                if (!baseArrays.TryGetValue(path, out var baseArray))
                {
                    // Nothing underneath to layer through.
                    continue;
                }

                if (baseArray.Count > overlayArray.Count)
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Warning,
                        $"Environment '{envName}' overrides array '{path}' with {overlayArray.Count} element(s), but the base array has {baseArray.Count}. " +
                        ".NET merges arrays by index across appsettings files, so the effective runtime value keeps the base's extra trailing element(s) instead of replacing the array. " +
                        $"Either keep '{path}' at least as long in this environment, or define it only per environment (not in the base) so nothing layers under it."));
                }

                var leaks = FindLeakedFields(baseArray, overlayArray);

                if (leaks.Count > 0)
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Warning,
                        $"Environment '{envName}' overrides array '{path}', whose elements are objects. " +
                        "At a shared index .NET merges those objects field by field, so every field the base element defines and this one omits survives at runtime: " +
                        $"{string.Join(", ", leaks)}. " +
                        $"Either repeat those fields here, or define '{path}' only per environment (not in the base) so nothing layers under it."));
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    ///     For each index present in both arrays, the fields of the base element that the
    ///     override does not redefine — the ones that leak. Reported as
    ///     <c>[index] field</c>, ordered by index then field.
    /// </summary>
    private static List<string> FindLeakedFields(JsonArray baseArray, JsonArray overlayArray)
    {
        var leaks = new List<string>();
        var shared = Math.Min(baseArray.Count, overlayArray.Count);

        for (var index = 0; index < shared; index++)
        {
            if (baseArray[index] is not JsonObject baseElement || overlayArray[index] is not JsonObject overlayElement)
            {
                // A primitive at this index is genuinely overwritten by the override.
                continue;
            }

            var overlayFields = CollectLeafPaths(overlayElement);

            foreach (var field in CollectLeafPaths(baseElement).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (!overlayFields.Contains(field))
                {
                    leaks.Add($"[{index}] {field}");
                }
            }
        }

        return leaks;
    }

    /// <summary>
    ///     Maps the dotted path of every array-valued node to the array itself.
    ///     Arrays are not recursed into, because .NET layers them at the array level.
    ///     Paths are compared case-insensitively: configuration keys are, so
    ///     <c>Svcs</c> in the base and <c>svcs</c> in an overlay are one key at runtime.
    /// </summary>
    private static Dictionary<string, JsonArray> CollectArrays(JsonObject settings)
    {
        var arrays = new Dictionary<string, JsonArray>(StringComparer.OrdinalIgnoreCase);
        Collect(settings, string.Empty, arrays);
        return arrays;
    }

    private static void Collect(JsonObject settings, string prefix, Dictionary<string, JsonArray> arrays)
    {
        foreach (var (key, value) in settings)
        {
            var path = prefix.Length == 0 ? key : $"{prefix}.{key}";

            switch (value)
            {
                case JsonArray array:
                    arrays[path] = array;
                    break;
                case JsonObject child:
                    Collect(child, path, arrays);
                    break;
            }
        }
    }

    /// <summary>
    ///     Dotted paths of the leaf values inside one array element. Nested arrays are
    ///     leaves here: what matters is whether the override redefines the entry at all.
    /// </summary>
    private static HashSet<string> CollectLeafPaths(JsonObject element)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectLeaves(element, string.Empty, paths);
        return paths;
    }

    private static void CollectLeaves(JsonObject settings, string prefix, HashSet<string> paths)
    {
        foreach (var (key, value) in settings)
        {
            var path = prefix.Length == 0 ? key : $"{prefix}.{key}";

            if (value is JsonObject child)
            {
                CollectLeaves(child, path, paths);
            }
            else
            {
                paths.Add(path);
            }
        }
    }
}
