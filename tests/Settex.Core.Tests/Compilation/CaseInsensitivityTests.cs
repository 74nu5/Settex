namespace Settex.Core.Tests.Compilation;

using Settex.Compilation;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// .NET configuration keys — and .NET's own environment model, whose
/// <c>IsDevelopment</c>/<c>IsEnvironment</c> compare with <c>OrdinalIgnoreCase</c> —
/// are case-insensitive. Every comparison Settex makes about them has to be too, or it
/// reasons about a different program than the one that will run.
/// </summary>
public class CaseInsensitivityTests
{
    /// <summary>
    /// This originally asserted that the two merge into one file. Merging removed the
    /// data loss they used to cause, but only by moving the failure: the generated file
    /// is named after one spelling, so on a case-sensitive filesystem
    /// ASPNETCORE_ENVIRONMENT=dev then loaded nothing at all. Two environments differing
    /// only in case have no workable meaning, so the pair is rejected.
    /// </summary>
    [Test]
    public async Task Compile_TwoEnvironmentsDifferingOnlyInCase_IsRejectedAsync()
    {
        const string source = """
            settings { A = 1 }
            env "Dev" { settings { A = 100 } }
            env "dev" { settings { B = 200 } }
            """;

        var (result, _, tempDir) = Compile(source);

        try
        {
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(e => e.Message.Contains("differ only in case"))).IsTrue();
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Test]
    public async Task Compile_SetIfMissingAgainstADifferentlySpelledBaseKey_DoesNotAssignAsync()
    {
        // ':=' means "only if absent". It compared ordinally, so it could not see the
        // base key and assigned anyway — the one operator whose entire contract is not
        // to override was the one overriding.
        const string source = """
            settings {
                Timeout = 30
                Db { Host = "base" }
            }
            env "Prod" {
                settings {
                    timeout := 99
                    db { Host := "prod" }
                }
            }
            """;

        var (result, outputDir, tempDir) = Compile(source);

        try
        {
            await Assert.That(result.Success).IsTrue();

            var overlay = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.Prod.json"));

            await Assert.That(overlay).DoesNotContain("99");
            await Assert.That(overlay).DoesNotContain("prod");
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Test]
    public async Task Compile_SetIfMissingAgainstAnAbsentKey_StillAssignsAsync()
    {
        // The guard in the other direction: a case-insensitive lookup must not make
        // ':=' stop assigning when the key genuinely is missing.
        const string source = """
            settings { Timeout = 30 }
            env "Prod" { settings { Retries := 5 } }
            """;

        var (result, outputDir, tempDir) = Compile(source);

        try
        {
            await Assert.That(result.Success).IsTrue();

            var overlay = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.Prod.json"));

            await Assert.That(overlay).Contains("5");
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    private static (CompilationResult Result, string OutputDir, string TempDir) Compile(string source)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SettexCaseTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var sourceFile = Path.Combine(tempDir, "appsettings.settex");
        var outputDir = Path.Combine(tempDir, "output");
        File.WriteAllText(sourceFile, source);

        return (new SettexCompiler().Compile(sourceFile, outputDir), outputDir, tempDir);
    }

    private static void Cleanup(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
