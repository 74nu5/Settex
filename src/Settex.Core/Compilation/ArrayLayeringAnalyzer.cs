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

                // One rule, derived from what the provider actually does rather than from
                // heuristics about lengths and element kinds: every base entry whose
                // flattened key the override does not redefine survives at runtime.
                var leaks = FindLeakedEntries(baseArray, overlayArray);

                if (leaks.Count > 0)
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Warning,
                        $"Environment '{envName}' overrides array '{path}', but .NET merges arrays by index across appsettings files rather than replacing them, " +
                        $"so these base entries survive at runtime: {string.Join(", ", leaks)}. " +
                        $"Either redefine them in this environment, or define '{path}' only per environment (not in the base) so nothing layers under it.",
                        keyPath: path,
                        environmentName: envName));
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    ///     The base entries an override does not redefine — the ones that leak.
    ///     <para>
    ///     .NET does not replace an array: it flattens every file into one key/value
    ///     dictionary (<c>Svcs:0:Port</c>) and layers the entries. So the question is not
    ///     how long each array is, nor whether its elements are objects — it is simply
    ///     which flattened keys the override redefines. Comparing the two flattened key
    ///     sets answers every shape at once: trailing indices left behind by a shorter
    ///     override, fields omitted from an object element, arrays nested inside an
    ///     element, and an object element replaced by a primitive (whose fields survive
    ///     underneath it). The earlier length-and-shape heuristics missed the last three.
    ///     </para>
    /// </summary>
    private static List<string> FindLeakedEntries(JsonArray baseArray, JsonArray overlayArray)
    {
        var overlayKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectEntries(overlayArray, string.Empty, overlayKeys);

        var baseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectEntries(baseArray, string.Empty, baseKeys);

        return baseKeys
            .Where(key => !overlayKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    ///     Flattens an array the way the configuration provider does: one entry per leaf
    ///     value, keyed by its index path (<c>[0].Tls.Cert</c>, <c>[1]</c>).
    /// </summary>
    private static void CollectEntries(JsonArray array, string prefix, HashSet<string> entries)
    {
        for (var index = 0; index < array.Count; index++)
        {
            CollectEntries(array[index], $"{prefix}[{index}]", entries);
        }
    }

    private static void CollectEntries(JsonNode? node, string prefix, HashSet<string> entries)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    CollectEntries(value, $"{prefix}.{key}", entries);
                }

                break;

            case JsonArray nested:
                CollectEntries(nested, prefix, entries);
                break;

            default:
                entries.Add(prefix);
                break;
        }
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
}
