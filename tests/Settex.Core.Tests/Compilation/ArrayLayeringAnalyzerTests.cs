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
}
