namespace Settex.Core.Tests.Compilation;

using System.Text.Json.Nodes;

using Settex.Compilation;
using Settex.Core.Evaluation;

using TUnit.Core;

/// <summary>
/// Tests for the array-layering check that warns when an environment shortens an
/// array that also exists in the base — the case where .NET's index-based array
/// merging silently keeps the base's extra trailing elements.
/// </summary>
public class ArrayLayeringAnalyzerTests
{
    [Test]
    public async Task Analyze_EnvShortensBaseArray_WarnsAsync()
    {
        var model = new SettingsModel(
            new JsonObject { ["Hosts"] = new JsonArray("a", "b", "c") },
            new()
            {
                ["Production"] = new JsonObject { ["Hosts"] = new JsonArray("x") },
            });

        var diagnostics = ArrayLayeringAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics[0].Message).Contains("Hosts");
        await Assert.That(diagnostics[0].Message).Contains("Production");
    }

    [Test]
    public async Task Analyze_SameLengthOverride_DoesNotWarnAsync()
    {
        var model = new SettingsModel(
            new JsonObject { ["Hosts"] = new JsonArray("a", "b", "c") },
            new()
            {
                ["Staging"] = new JsonObject { ["Hosts"] = new JsonArray("x", "y", "z") },
            });

        var diagnostics = ArrayLayeringAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_LongerOverride_DoesNotWarnAsync()
    {
        var model = new SettingsModel(
            new JsonObject { ["Hosts"] = new JsonArray("a", "b") },
            new()
            {
                ["Production"] = new JsonObject { ["Hosts"] = new JsonArray("x", "y", "z") },
            });

        var diagnostics = ArrayLayeringAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_ArrayOnlyInEnvironment_DoesNotWarnAsync()
    {
        // No base array to layer under → no leak possible.
        var model = new SettingsModel(
            new JsonObject(),
            new()
            {
                ["Production"] = new JsonObject { ["Hosts"] = new JsonArray("x") },
            });

        var diagnostics = ArrayLayeringAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_NestedArrayShortened_WarnsWithDottedPathAsync()
    {
        var model = new SettingsModel(
            new JsonObject { ["Server"] = new JsonObject { ["Ports"] = new JsonArray(1, 2, 3) } },
            new()
            {
                ["Production"] = new JsonObject { ["Server"] = new JsonObject { ["Ports"] = new JsonArray(9) } },
            });

        var diagnostics = ArrayLayeringAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Message).Contains("Server.Ports");
    }
    /// <summary>
    /// The leak an element count cannot see. Both arrays hold one object, so no length
    /// rule fires — but .NET merges the two elements field by field, so a field the
    /// base defines and the override omits survives. Verified against a real
    /// ConfigurationBuilder, which reports Svcs:0:Port = 1.
    /// </summary>
    [Test]
    public async Task Analyze_ObjectElementsOfEqualLength_WarnsAboutTheOmittedFieldsAsync()
    {
        var model = new SettingsModel(
            new JsonObject
            {
                ["Svcs"] = new JsonArray(new JsonObject { ["Name"] = "a", ["Port"] = 1 }),
            },
            new()
            {
                ["Dev"] = new JsonObject
                {
                    ["Svcs"] = new JsonArray(new JsonObject { ["Name"] = "b" }),
                },
            });

        var diagnostics = ArrayLayeringAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics[0].Message).Contains("[0] Port");
        await Assert.That(diagnostics[0].Message).DoesNotContain("Name");
    }

    [Test]
    public async Task Analyze_ObjectElementsRedefiningEveryField_DoesNotWarnAsync()
    {
        var model = new SettingsModel(
            new JsonObject
            {
                ["Svcs"] = new JsonArray(new JsonObject { ["Name"] = "a", ["Port"] = 1 }),
            },
            new()
            {
                ["Dev"] = new JsonObject
                {
                    ["Svcs"] = new JsonArray(new JsonObject { ["Name"] = "b", ["Port"] = 2 }),
                },
            });

        var diagnostics = ArrayLayeringAnalyzer.Analyze(model);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// Nested fields leak the same way, and must be named by their full path.
    /// </summary>
    [Test]
    public async Task Analyze_ObjectElementsWithNestedFields_NamesTheFullPathAsync()
    {
        var model = new SettingsModel(
            new JsonObject
            {
                ["Svcs"] = new JsonArray(new JsonObject
                {
                    ["Tls"] = new JsonObject { ["Enabled"] = true, ["Cert"] = "base.pem" },
                }),
            },
            new()
            {
                ["Dev"] = new JsonObject
                {
                    ["Svcs"] = new JsonArray(new JsonObject
                    {
                        ["Tls"] = new JsonObject { ["Enabled"] = false },
                    }),
                },
            });

        var diagnostics = ArrayLayeringAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Message).Contains("[0] Tls.Cert");
    }

    /// <summary>
    /// .NET configuration keys are case-insensitive, so a base 'Cased' and an overlay
    /// 'cased' are one key at runtime — and the shorter overlay does leak. Comparing
    /// ordinally made the analyzer treat them as unrelated and stay silent.
    /// </summary>
    [Test]
    public async Task Analyze_PathsDifferingOnlyInCase_AreTreatedAsOneKeyAsync()
    {
        var model = new SettingsModel(
            new JsonObject { ["Cased"] = new JsonArray(1, 2, 3) },
            new()
            {
                ["Dev"] = new JsonObject { ["cased"] = new JsonArray(7) },
            });

        var diagnostics = ArrayLayeringAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Message).Contains("cased");
    }
}
