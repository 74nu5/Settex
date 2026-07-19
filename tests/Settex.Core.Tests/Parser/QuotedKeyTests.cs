namespace Settex.Core.Tests.Parser;

using Settex.Compilation;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// The dot is this language's path separator, so a key that itself contains a dot was
/// inexpressible: <c>Microsoft.AspNetCore</c> written bare became two nested keys, and
/// the log-level filter every ASP.NET Core app sets was silently discarded — .NET
/// flattens with a colon and treats a dot as an ordinary character, so it needs
/// <c>Logging:LogLevel:Microsoft.AspNetCore</c> as one key. Quoting a path segment is
/// how that is now written.
/// </summary>
public class QuotedKeyTests
{
    [Test]
    public async Task Compile_QuotedSegmentContainingADot_StaysOneKeyAsync()
    {
        const string source = """
            settings {
                Logging {
                    LogLevel {
                        Default = "Information"
                        "Microsoft.AspNetCore" = "Warning"
                    }
                }
            }
            """;

        var json = await CompileAndReadAsync(source);

        await Assert.That(json).Contains("\"Microsoft.AspNetCore\": \"Warning\"");

        // The bare form must still nest, or existing configurations would change meaning.
        await Assert.That(json).DoesNotContain("\"Microsoft\": {");
    }

    [Test]
    public async Task Compile_BareDottedPath_StillNestsAsync()
    {
        const string source = "settings {\n    A.B.C = 1\n}";

        var json = await CompileAndReadAsync(source);

        await Assert.That(json).Contains("\"A\"");
        await Assert.That(json).Contains("\"B\"");
        await Assert.That(json).DoesNotContain("\"A.B.C\"");
    }

    [Test]
    public async Task Compile_QuotedSegmentInTheMiddleOfAPath_IsHonouredAsync()
    {
        const string source = "settings {\n    Nested.\"A.B\".C = 1\n}";

        var json = await CompileAndReadAsync(source);

        await Assert.That(json).Contains("\"A.B\"");
    }

    [Test]
    public async Task Compile_QuotedKeyWithOtherPunctuation_IsHonouredAsync()
    {
        const string source = "settings {\n    \"Content-Type\" = \"application/json\"\n}";

        var json = await CompileAndReadAsync(source);

        await Assert.That(json).Contains("\"Content-Type\"");
    }

    [Test]
    public async Task Compile_QuotedNestedBlockName_IsHonouredAsync()
    {
        const string source = "settings {\n    \"My.Section\" {\n        A = 1\n    }\n}";

        var json = await CompileAndReadAsync(source);

        await Assert.That(json).Contains("\"My.Section\"");
    }

    [Test]
    public async Task Compile_InterpolationInAKey_IsRejectedAsync()
    {
        // A key is part of the file's structure and has to be known before anything is
        // evaluated. Treating "${x}" as a literal key would be a trap, so it is refused
        // rather than guessed at.
        const string source = "let x = 1\nsettings {\n    \"${x}\" = 1\n}";

        var (result, tempDir) = Compile(source);

        try
        {
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(e => e.Message.Contains("interpolation"))).IsTrue();
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Test]
    public async Task Compile_EmptyQuotedKey_IsRejectedAsync()
    {
        var (result, tempDir) = Compile("settings {\n    \"\" = 1\n}");

        try
        {
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(e => e.Message.Contains("empty"))).IsTrue();
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    private static async Task<string> CompileAndReadAsync(string source)
    {
        var (result, tempDir) = Compile(source);

        try
        {
            await Assert.That(result.Success).IsTrue();

            return await File.ReadAllTextAsync(Path.Combine(tempDir, "output", "appsettings.json"));
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    private static (CompilationResult Result, string TempDir) Compile(string source)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SettexQuotedKeys", Guid.NewGuid().ToString());
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
