namespace Settex.Compilation;

using System.Text.Json.Nodes;

using Settex.Core.Evaluation;

/// <summary>
///     Warns about the .NET array-layering trap: when an environment overrides an
///     array that also exists in the base with <strong>fewer</strong> elements, .NET's
///     configuration provider merges the two files <em>by index</em> rather than
///     replacing the array — so the effective runtime value keeps the base's extra
///     trailing elements. Settex cannot change how the .NET provider layers arrays,
///     so it surfaces the hazard at compile time instead of letting it fail silently.
/// </summary>
public static class ArrayLayeringAnalyzer
{
    /// <summary>
    ///     Returns a warning for every environment array override that would leak
    ///     base elements at runtime. Advisory only — never an error.
    /// </summary>
    public static IReadOnlyList<Diagnostic> Analyze(SettingsModel model)
    {
        var diagnostics = new List<Diagnostic>();
        var baseArrays = CollectArrayLengths(model.BaseSettings);

        foreach (var (envName, overlay) in model.EnvironmentOverlays)
        {
            var overlayArrays = CollectArrayLengths(overlay);

            // Deterministic order for stable diagnostics.
            foreach (var path in overlayArrays.Keys.OrderBy(p => p, StringComparer.Ordinal))
            {
                var overlayLength = overlayArrays[path];

                // A leak happens only when the base array is longer than the override:
                // the override overwrites indices 0..n-1, and the base's remaining
                // indices survive. Equal or longer overrides replace every index.
                if (baseArrays.TryGetValue(path, out var baseLength) && baseLength > overlayLength)
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Warning,
                        $"Environment '{envName}' overrides array '{path}' with {overlayLength} element(s), but the base array has {baseLength}. " +
                        ".NET merges arrays by index across appsettings files, so the effective runtime value keeps the base's extra trailing element(s) instead of replacing the array. " +
                        $"Either keep '{path}' at least as long in this environment, or define it only per environment (not in the base) so nothing layers under it."));
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    ///     Maps the dotted path of every array-valued node to its element count.
    ///     Arrays are treated as leaves (not recursed into), because .NET layers at
    ///     the array level.
    /// </summary>
    private static Dictionary<string, int> CollectArrayLengths(JsonObject settings)
    {
        var arrays = new Dictionary<string, int>(StringComparer.Ordinal);
        Collect(settings, string.Empty, arrays);
        return arrays;
    }

    private static void Collect(JsonObject settings, string prefix, Dictionary<string, int> arrays)
    {
        foreach (var (key, value) in settings)
        {
            var path = prefix.Length == 0 ? key : $"{prefix}.{key}";

            switch (value)
            {
                case JsonArray array:
                    arrays[path] = array.Count;
                    break;
                case JsonObject child:
                    Collect(child, path, arrays);
                    break;
            }
        }
    }
}
