namespace Settex.Core.Tests.Compilation;

using System.Text.Json.Nodes;

using Settex.Compilation;
using Settex.Core.Evaluation;

using TUnit.Core;

/// <summary>
/// Tests for the cross-environment coverage check that warns about keys defined
/// for some environments but missing from others (and from the base).
/// </summary>
public class CoverageAnalyzerTests
{
    [Test]
    public async Task Analyze_EnvOnlyKeyMissingFromAnotherEnv_WarnsAsync()
    {
        var model = new SettingsModel(
            new JsonObject(),
            new()
            {
                ["Development"] = new JsonObject { ["DevOnly"] = new JsonObject { ["Flag"] = true } },
                ["Production"] = new JsonObject(),
            });

        var diagnostics = CoverageAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics[0].Message).Contains("DevOnly.Flag");
        await Assert.That(diagnostics[0].Message).Contains("Development");
        await Assert.That(diagnostics[0].Message).Contains("Production");
    }

    [Test]
    public async Task Analyze_KeyPresentInBase_DoesNotWarnAsync()
    {
        // Shared lives in the base, so it is inherited by every environment.
        var model = new SettingsModel(
            new JsonObject { ["Shared"] = 1 },
            new()
            {
                ["Development"] = new JsonObject { ["Shared"] = 2 },
                ["Production"] = new JsonObject(),
            });

        var diagnostics = CoverageAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_KeyDefinedInAllEnvironments_DoesNotWarnAsync()
    {
        // Deliberate: the config is correct as it stands, and hoisting such a key
        // into the base would mean inventing a default that applies silently.
        var model = new SettingsModel(
            new JsonObject(),
            new()
            {
                ["Development"] = new JsonObject { ["Key"] = 1 },
                ["Production"] = new JsonObject { ["Key"] = 2 },
            });

        var diagnostics = CoverageAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_NewEnvironmentAddedWithoutTheKey_WarnsAsync()
    {
        // This is why the case above can stay silent: the deferred risk — "someone
        // adds an environment and forgets the key" — is caught the moment it becomes
        // real, because the key is then in some environments but not all.
        var model = new SettingsModel(
            new JsonObject(),
            new()
            {
                ["Development"] = new JsonObject { ["Key"] = 1 },
                ["Production"] = new JsonObject { ["Key"] = 2 },
                ["Staging"] = new JsonObject { ["Other"] = 3 },
            });

        var diagnostics = CoverageAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Any(d => d.Message.Contains("'Key'")
            && d.Message.Contains("Staging"))).IsTrue();
    }

    [Test]
    public async Task Analyze_SingleEnvironment_DoesNotWarnAsync()
    {
        var model = new SettingsModel(
            new JsonObject(),
            new()
            {
                ["Development"] = new JsonObject { ["Key"] = 1 },
            });

        var diagnostics = CoverageAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Analyze_NestedDriftInBothDirections_WarnsForEachAsync()
    {
        var model = new SettingsModel(
            new JsonObject(),
            new()
            {
                ["Development"] = new JsonObject { ["Server"] = new JsonObject { ["Timeout"] = 30 } },
                ["Production"] = new JsonObject { ["Server"] = new JsonObject { ["Host"] = "prod" } },
            });

        var diagnostics = CoverageAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Count).IsEqualTo(2);
        await Assert.That(diagnostics.Any(d => d.Message.Contains("Server.Timeout"))).IsTrue();
        await Assert.That(diagnostics.Any(d => d.Message.Contains("Server.Host"))).IsTrue();
    }
    /// <summary>
    /// A key spelled 'Timeout' in the base and 'timeout' in an environment is one key
    /// at runtime, so the environment is covered and there is no drift. Comparing
    /// ordinally produced a warning that was not merely noisy but actively misleading:
    /// it announced a missing key when the real story was a spelling disagreement.
    /// </summary>
    [Test]
    public async Task Analyze_EnvKeyDifferingFromBaseOnlyInCase_DoesNotWarnAsync()
    {
        var model = new SettingsModel(
            new JsonObject { ["Timeout"] = 30 },
            new()
            {
                ["Dev"] = new JsonObject { ["timeout"] = 5 },
                ["Prod"] = new JsonObject { ["Other"] = 1 },
            });

        var diagnostics = CoverageAnalyzer.Analyze(model);

        await Assert.That(diagnostics.Any(d => d.Message.Contains("imeout"))).IsFalse();
    }

    /// <summary>
    /// Two environments spelling the same environment-only key differently cover each
    /// other, so nothing is missing — and in any case only one candidate exists, not
    /// two producing duplicate warnings.
    /// </summary>
    [Test]
    public async Task Analyze_EnvironmentsSpellingTheSameKeyDifferently_DoNotDriftAsync()
    {
        var model = new SettingsModel(
            new JsonObject(),
            new()
            {
                ["Dev"] = new JsonObject { ["Feature"] = true },
                ["Prod"] = new JsonObject { ["feature"] = false },
            });

        var diagnostics = CoverageAnalyzer.Analyze(model);

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// Environment names are quoted in the message, so their order must not depend on
    /// Dictionary enumeration.
    /// </summary>
    [Test]
    public async Task Analyze_QuotesEnvironmentsInASortedOrderAsync()
    {
        var model = new SettingsModel(
            new JsonObject(),
            new()
            {
                ["Zulu"] = new JsonObject { ["Only"] = 1 },
                ["Alpha"] = new JsonObject { ["Other"] = 2 },
                ["Mike"] = new JsonObject { ["Other"] = 3 },
            });

        var diagnostics = CoverageAnalyzer.Analyze(model);

        var onlyWarning = diagnostics.Single(d => d.Message.Contains("'Only'"));

        await Assert.That(onlyWarning.Message).Contains("missing from 'Alpha', 'Mike'");
    }
}
