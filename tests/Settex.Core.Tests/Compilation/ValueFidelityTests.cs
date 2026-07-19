namespace Settex.Core.Tests.Compilation;

using Settex.Compilation;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Three invariants about how values reach the generated file. Each is a deliberate
/// one-line choice in the production code, and mutation testing showed that changing
/// any of them left all 381 tests green — so a one-character regression would ship
/// silently corrupted configuration.
/// </summary>
public class ValueFidelityTests
{
    [Test]
    public async Task Compile_FractionalNumberInInterpolationAndConcat_UsesAnInvariantDecimalPointAsync()
    {
        // Formatting with the current culture instead of the invariant one produces
        // "rate=1,5" on any comma-decimal machine — French, German, Spanish, and most
        // CI agents configured for them. Every existing interpolation test used whole
        // numbers, where the two cultures agree, so nothing caught it.
        const string source = """
            let rate = 1.5
            settings {
                Interpolated = "rate=${rate}"
                Concatenated = "rate=" + rate
            }
            """;

        var json = await CompileAndReadAsync(source);

        await Assert.That(json).Contains("rate=1.5");
        await Assert.That(json).DoesNotContain("rate=1,5");
    }

    [Test]
    public async Task Compile_ValuesNeedingHtmlEscaping_AreWrittenAsIsAsync()
    {
        // The writer uses the relaxed JSON encoder on purpose. The stricter default
        // escapes &, < and > and every non-ASCII character, which would turn ordinary
        // connection strings and URLs into unreadable & soup in every generated
        // file. Nothing pinned that choice.
        const string source = """
            settings {
                Query = "a & b < c > d"
                Accented = "é"
            }
            """;

        var json = await CompileAndReadAsync(source);

        await Assert.That(json).Contains("a & b < c > d");
        await Assert.That(json).Contains("é");
        await Assert.That(json).DoesNotContain("\\u0026");
    }

    [Test]
    public async Task Compile_StringEquality_IsCaseSensitiveAsync()
    {
        // Core language semantics, and the basis of every `env == "Production"` guard.
        // Making the comparison case-insensitive would change which conditional
        // assignments fire in every user's configuration, undetected.
        const string source = """
            settings {
                SameCase = "yes" if "abc" == "abc"
                MixedCase = "yes" if "ABC" == "abc"
            }
            """;

        var json = await CompileAndReadAsync(source);

        await Assert.That(json).Contains("SameCase");
        await Assert.That(json).DoesNotContain("MixedCase");
    }

    private static async Task<string> CompileAndReadAsync(string source)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SettexValueFidelity", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceFile = Path.Combine(tempDir, "appsettings.settex");
            var outputDir = Path.Combine(tempDir, "output");
            await File.WriteAllTextAsync(sourceFile, source);

            var result = new SettexCompiler().Compile(sourceFile, outputDir);

            await Assert.That(result.Success).IsTrue();

            return await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
