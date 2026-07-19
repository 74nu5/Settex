namespace Settex.Core.Importing;

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

using Settex.Core.Evaluation;
using Settex.Core.Lexer;
using Settex.Core.Merging;
using Settex.Core.Parser;

/// <summary>
///     Turns an existing <c>appsettings.json</c> family into a <c>.settex</c> file, and
///     proves the result equivalent before anyone commits to it.
///     <para>
///     This is the adoption path: nobody rewrites a working production configuration by
///     hand, and a migration that is merely <em>probably</em> right is worse than none —
///     a missed key surfaces at runtime, in the environment where it was missed. So
///     <see cref="VerifyRoundTrip" /> compiles the generated text through the real
///     pipeline and compares the flattened key/value sets, the way .NET's configuration
///     provider sees them, against the original files. Import is only reported
///     successful when that comparison is exact.
///     </para>
/// </summary>
public static class JsonImporter
{
    /// <summary>
    ///     Keys that must be quoted even though they look like identifiers, because the
    ///     lexer turns them into keywords and the parser would refuse the statement.
    ///     A real configuration can legitimately contain a key named <c>env</c>.
    /// </summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "settings", "env", "true", "false", "null", "include",
        "let", "and", "or", "not", "if", "for", "in",
    };

    /// <summary>
    ///     Generates the <c>.settex</c> source for a base settings object and its
    ///     per-environment overlays.
    /// </summary>
    public static string GenerateSettex(JsonObject baseSettings, IReadOnlyDictionary<string, JsonObject> environments)
    {
        var builder = new StringBuilder();

        builder.AppendLine("settings {");
        EmitObject(builder, baseSettings, 1);
        builder.AppendLine("}");

        foreach (var (name, overlay) in environments.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine($"env \"{EscapeString(name)}\" {{");
            builder.AppendLine("    settings {");
            EmitObject(builder, overlay, 2);
            builder.AppendLine("    }");
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Compiles <paramref name="settexSource" /> through the real pipeline and
    ///     compares the effective configuration of every environment — and of the base
    ///     alone — against the original JSON, flattened the way .NET flattens it
    ///     (<c>a:b:0:c</c>) and compared the way .NET compares it (case-insensitively).
    ///     Returns every difference; an empty list is the only acceptable outcome.
    /// </summary>
    public static IReadOnlyList<string> VerifyRoundTrip(
        string settexSource,
        JsonObject originalBase,
        IReadOnlyDictionary<string, JsonObject> originalEnvironments)
    {
        var tokens = new Lexer(settexSource).Tokenize();
        var ast = new Parser(tokens).Parse();
        var model = new Evaluator().Evaluate(ast);

        var differences = new List<string>();
        var merger = new Merger();

        Compare(differences, "base", Flatten(originalBase), Flatten(model.BaseSettings));

        foreach (var (name, originalOverlay) in originalEnvironments)
        {
            if (!model.EnvironmentOverlays.TryGetValue(name, out var overlay))
            {
                differences.Add($"environment '{name}': missing from the generated file");
                continue;
            }

            // What .NET sees at runtime is base layered with the overlay, so that is
            // what has to match — not the overlay in isolation, whose exact shape is
            // an implementation detail of the delta.
            var originalEffective = merger.Merge(originalBase, originalOverlay);
            var generatedEffective = merger.Merge(model.BaseSettings, overlay);

            Compare(differences, $"environment '{name}'", Flatten(originalEffective), Flatten(generatedEffective));
        }

        return differences;
    }

    private static void Compare(
        List<string> differences,
        string context,
        Dictionary<string, string> original,
        Dictionary<string, string> generated)
    {
        foreach (var (key, value) in original)
        {
            if (!generated.TryGetValue(key, out var actual))
            {
                differences.Add($"{context}: key '{key}' is missing from the generated configuration");
            }
            else if (!string.Equals(value, actual, StringComparison.Ordinal))
            {
                differences.Add($"{context}: key '{key}' is '{value}' in the original but '{actual}' when generated");
            }
        }

        foreach (var key in generated.Keys)
        {
            if (!original.ContainsKey(key))
            {
                differences.Add($"{context}: key '{key}' appears in the generated configuration but not in the original");
            }
        }
    }

    /// <summary>
    ///     Flattens a JSON object into the key/value pairs .NET configuration builds
    ///     from it: colon-separated paths, array elements by index, and leaf values by
    ///     their JSON text.
    /// </summary>
    private static Dictionary<string, string> Flatten(JsonObject settings)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FlattenInto(settings, string.Empty, entries);
        return entries;
    }

    private static void FlattenInto(JsonNode? node, string prefix, Dictionary<string, string> entries)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    FlattenInto(value, prefix.Length == 0 ? key : $"{prefix}:{key}", entries);
                }

                break;

            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    FlattenInto(array[i], $"{prefix}:{i}", entries);
                }

                break;

            case null:
                entries[prefix] = "null";
                break;

            default:
                entries[prefix] = node.ToJsonString();
                break;
        }
    }

    private static void EmitObject(StringBuilder builder, JsonObject obj, int indent)
    {
        var pad = new string(' ', indent * 4);

        foreach (var (rawKey, value) in obj)
        {
            var key = EmitKey(rawKey);

            switch (value)
            {
                case JsonObject child when child.Count > 0:
                    builder.AppendLine($"{pad}{key} {{");
                    EmitObject(builder, child, indent + 1);
                    builder.AppendLine($"{pad}}}");
                    break;

                case JsonObject:
                    builder.AppendLine($"{pad}{key} {{ }}");
                    break;

                case JsonArray array when array.Count == 0:
                    builder.AppendLine($"{pad}{key} = []");
                    break;

                case JsonArray array:
                    builder.AppendLine($"{pad}{key} = [");

                    foreach (var element in array)
                    {
                        if (element is JsonObject elementObject)
                        {
                            builder.AppendLine($"{pad}    item {{");
                            EmitObject(builder, elementObject, indent + 2);
                            builder.AppendLine($"{pad}    }}");
                        }
                        else
                        {
                            builder.AppendLine($"{pad}    {EmitValue(element)}");
                        }
                    }

                    builder.AppendLine($"{pad}]");
                    break;

                default:
                    builder.AppendLine($"{pad}{key} = {EmitValue(value)}");
                    break;
            }
        }
    }

    private static string EmitKey(string key)
    {
        // A key containing "${" cannot be represented: the parser refuses an
        // interpolation in a key, including the escaped form, because a key has to be
        // known before anything is evaluated. Refuse loudly rather than emit a file
        // that cannot compile.
        if (key.Contains("${", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"The key '{key}' contains \"${{\", which a Settex configuration key cannot express.");
        }

        var isPlainIdentifier = key.Length > 0
            && (char.IsLetter(key[0]) || key[0] == '_')
            && key.All(c => char.IsLetterOrDigit(c) || c == '_')
            && !Keywords.Contains(key);

        return isPlainIdentifier ? key : $"\"{EscapeString(key)}\"";
    }

    private static string EmitValue(JsonNode? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value.GetValueKind() is System.Text.Json.JsonValueKind.String)
        {
            var text = value.GetValue<string>();

            // A literal "${" would otherwise be interpolated at compile time.
            return $"\"{EscapeString(text).Replace("${", "$${", StringComparison.Ordinal)}\"";
        }

        // Numbers and booleans: the JSON text is already the Settex literal.
        return value.ToJsonString();
    }

    private static string EscapeString(string text)
        => text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
}
