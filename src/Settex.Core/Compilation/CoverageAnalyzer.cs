namespace Settex.Compilation;

using System.Text.Json.Nodes;

using Settex.Core.Evaluation;

/// <summary>
///     Flags configuration drift: keys introduced for some environments but missing
///     from others (and from the base settings). This is the mistake Settex is meant
///     to prevent — a key added to one environment's config and forgotten in the
///     rest. A key present in the base settings is inherited by every environment and
///     is therefore never flagged.
///
///     <para>
///     <strong>Deliberate decision — a key set in <em>every</em> environment but not
///     in the base is not flagged.</strong> Three reasons, in order of weight:
///     </para>
///     <list type="number">
///       <item>
///         The configuration is <em>correct as it stands</em>: every environment has
///         the key. A linter should report what is wrong now, not what might become
///         wrong.
///       </item>
///       <item>
///         The obvious "fix" — hoist it into the base — can be actively harmful.
///         Values that must be decided per environment (connection strings, endpoints,
///         secret placeholders) are safer with <em>no</em> base default than with a
///         plausible-looking wrong one that applies silently.
///       </item>
///       <item>
///         The deferred risk is already covered. The hazard is "someone adds a new
///         environment and forgets the key" — and at that moment the key is in some
///         environments but not all, so the rule above fires. The safety net closes
///         exactly when the risk becomes real, which is why no warning is needed
///         before then. This is locked by a regression test.
///       </item>
///     </list>
/// </summary>
public static class CoverageAnalyzer
{
    /// <summary>
    ///     Returns a warning for every environment-only key that is not defined in
    ///     all environments. Never returns errors — coverage is advisory.
    /// </summary>
    public static IReadOnlyList<Diagnostic> Analyze(SettingsModel model)
    {
        var diagnostics = new List<Diagnostic>();
        var envNames = model.EnvironmentOverlays.Keys.ToList();

        // Drift is only meaningful across two or more environments.
        if (envNames.Count < 2)
        {
            return diagnostics;
        }

        var baseKeys = CollectLeafPaths(model.BaseSettings);
        var envKeys = model.EnvironmentOverlays.ToDictionary(
            kvp => kvp.Key,
            kvp => CollectLeafPaths(kvp.Value));

        // Every leaf key that appears in some environment but not in the base.
        var candidateKeys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var keys in envKeys.Values)
        {
            foreach (var key in keys)
            {
                if (!baseKeys.Contains(key))
                {
                    candidateKeys.Add(key);
                }
            }
        }

        foreach (var key in candidateKeys)
        {
            var definedIn = envNames.Where(env => envKeys[env].Contains(key)).ToList();
            var missingIn = envNames.Where(env => !envKeys[env].Contains(key)).ToList();

            if (missingIn.Count > 0)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    $"Key '{key}' is set in {Quote(definedIn)} but missing from {Quote(missingIn)}, and is not in the base settings. " +
                    "Add it to the base 'settings' block or to the missing environment(s) to keep configuration consistent."));
            }
        }

        return diagnostics;
    }

    private static string Quote(IEnumerable<string> names)
        => string.Join(", ", names.Select(name => $"'{name}'"));

    /// <summary>
    ///     Collects the dotted paths of all leaf values (non-objects) in a settings
    ///     object. Arrays and primitives are leaves; nested objects are recursed into.
    /// </summary>
    private static HashSet<string> CollectLeafPaths(JsonObject settings)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        Collect(settings, string.Empty, paths);
        return paths;
    }

    private static void Collect(JsonObject settings, string prefix, HashSet<string> paths)
    {
        foreach (var (key, value) in settings)
        {
            var path = prefix.Length == 0 ? key : $"{prefix}.{key}";

            if (value is JsonObject child)
            {
                Collect(child, path, paths);
            }
            else
            {
                paths.Add(path);
            }
        }
    }
}
