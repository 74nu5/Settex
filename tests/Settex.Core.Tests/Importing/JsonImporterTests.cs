namespace Settex.Core.Tests.Importing;

using System.Text.Json.Nodes;

using Settex.Core.Importing;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// The importer's contract is not "produces plausible Settex" but "produces Settex
/// proven equivalent": VerifyRoundTrip compiles the generated text through the real
/// pipeline and compares the flattened configuration with the original. These tests
/// feed it the shapes a real appsettings family contains, plus the ones designed to
/// break naive generation.
/// </summary>
public class JsonImporterTests
{
    [Test]
    public async Task Import_KitchenSink_RoundTripsExactlyAsync()
    {
        // Every shape that broke something at some point this campaign: keyword keys,
        // dotted keys, ${ in values, escapes, empty arrays and objects, arrays of
        // objects, null, numbers, deep nesting.
        var baseSettings = new JsonObject
        {
            ["env"] = "a-keyword-as-a-key",
            ["Microsoft.AspNetCore"] = "a-dotted-key",
            ["Price"] = "cost: ${price}",
            ["Path"] = "C:\\temp\\\"quoted\"\nline2\ttabbed",
            ["EmptyArray"] = new JsonArray(),
            ["EmptyObject"] = new JsonObject(),
            ["Rules"] = new JsonArray(
                new JsonObject { ["Endpoint"] = "post:*", ["Limit"] = 60 },
                new JsonObject { ["Endpoint"] = "get:*", ["Limit"] = 200 }),
            ["Nothing"] = null,
            ["Pi"] = 3.14,
            ["Big"] = long.MaxValue,
            ["Deep"] = new JsonObject
            {
                ["er"] = new JsonObject { ["est"] = new JsonArray("a", "b") },
            },
        };

        var environments = new Dictionary<string, JsonObject>
        {
            ["Development"] = new JsonObject
            {
                ["Price"] = "free",
                ["Deep"] = new JsonObject { ["er"] = new JsonObject { ["extra"] = true } },
            },
        };

        var settex = JsonImporter.GenerateSettex(baseSettings, environments);
        var differences = JsonImporter.VerifyRoundTrip(settex, baseSettings, environments);

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task Verify_CatchesAMissingKeyAsync()
    {
        // The verifier itself must be able to fail, or "verified exact" is a slogan.
        var baseSettings = new JsonObject { ["A"] = 1, ["B"] = 2 };

        var doctored = "settings {\n    A = 1\n}\n";
        var differences = JsonImporter.VerifyRoundTrip(doctored, baseSettings, new Dictionary<string, JsonObject>());

        await Assert.That(differences.Any(d => d.Contains("'B'"))).IsTrue();
    }

    [Test]
    public async Task Verify_CatchesAChangedValueAsync()
    {
        var baseSettings = new JsonObject { ["A"] = 1 };

        var doctored = "settings {\n    A = 2\n}\n";
        var differences = JsonImporter.VerifyRoundTrip(doctored, baseSettings, new Dictionary<string, JsonObject>());

        await Assert.That(differences.Any(d => d.Contains("'A'"))).IsTrue();
    }

    [Test]
    public async Task Import_KeyContainingAnInterpolation_IsRefusedLoudlyAsync()
    {
        // A Settex key cannot express "${" — the parser refuses interpolation in keys.
        // Refusing the import beats emitting a file that cannot compile.
        var baseSettings = new JsonObject { ["${weird}"] = 1 };

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            JsonImporter.GenerateSettex(baseSettings, new Dictionary<string, JsonObject>());
            await Task.CompletedTask;
        });
    }
}
