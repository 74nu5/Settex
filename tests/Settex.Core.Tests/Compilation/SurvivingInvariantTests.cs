namespace Settex.Core.Tests.Compilation;

using System.Text.Json.Nodes;

using Settex.Compilation;
using Settex.Core.Evaluation;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Four invariants that mutation testing showed no test protected: each could be
/// removed from the production code with all tests still green.
/// </summary>
public class SurvivingInvariantTests
{
    [Test]
    public async Task Compile_LogicalAnd_DoesNotEvaluateItsRightSideWhenTheLeftIsFalseAsync()
    {
        // Short-circuiting is written and commented in the evaluator but nothing checked
        // it. Observable only when the right side would fail: an undefined variable here
        // must never be reached.
        const string source = """
            settings {
                A = "yes" if false and undefinedVariable
            }
            """;

        var (result, tempDir) = Compile(source);

        try
        {
            await Assert.That(result.Success).IsTrue();
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Test]
    public async Task Compile_LogicalOr_DoesNotEvaluateItsRightSideWhenTheLeftIsTrueAsync()
    {
        const string source = """
            settings {
                A = "yes" if true or undefinedVariable
            }
            """;

        var (result, tempDir) = Compile(source);

        try
        {
            await Assert.That(result.Success).IsTrue();
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Test]
    public async Task Compile_TwoLetsOfTheSameName_AreRejectedAsync()
    {
        // A deliberate validation with a user-facing message, and no test anywhere.
        var (result, tempDir) = Compile("let a = 1\nlet a = 2\nsettings { A = a }");

        try
        {
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(e => e.Message.Contains("Duplicate"))).IsTrue();
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Test]
    public async Task Compile_GeneratedFiles_UseLineFeedEndingsAsync()
    {
        // The writer normalises to LF on purpose. Changing it would churn every diff on
        // every platform, and nothing pinned it.
        var (result, tempDir) = Compile("settings {\n    A = 1\n    B = 2\n}");

        try
        {
            await Assert.That(result.Success).IsTrue();

            var json = await File.ReadAllTextAsync(Path.Combine(tempDir, "output", "appsettings.json"));

            await Assert.That(json).Contains("\n");
            await Assert.That(json).DoesNotContain("\r\n");
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Test]
    public async Task Analyze_ArrayEntriesDifferingOnlyInCase_AreOneEntryAsync()
    {
        // The flattened entry keys are compared case-insensitively, like every other
        // configuration key. Comparing them ordinally would report a field as leaking
        // when the override does redefine it, only with different casing.
        var model = new SettingsModel(
            new JsonObject
            {
                ["Svcs"] = new JsonArray(new JsonObject { ["Name"] = "a", ["Port"] = 1 }),
            },
            new()
            {
                ["Dev"] = new JsonObject
                {
                    ["Svcs"] = new JsonArray(new JsonObject { ["name"] = "b", ["port"] = 2 }),
                },
            });

        var diagnostics = ArrayLayeringAnalyzer.Analyze(model);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static (CompilationResult Result, string TempDir) Compile(string source)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SettexInvariants", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var sourceFile = Path.Combine(tempDir, "appsettings.settex");
        File.WriteAllText(sourceFile, source);

        return (new SettexCompiler().Compile(sourceFile, Path.Combine(tempDir, "output")), tempDir);
    }

    private static void Cleanup(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
