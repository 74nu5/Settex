namespace Settex.Core.Tests.Compilation;

using Settex.Compilation;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Resolution;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// Nested-block merging, the distinction between an empty block and an emptied one,
/// and two diagnostics that were reported without a position.
/// </summary>
public class NestedBlockAndLocationTests
{
    [Test]
    public async Task Compile_TwoNestedBlocksOfTheSameName_DeepMergeAsync()
    {
        // The second block used to replace the first outright, losing Host with no
        // diagnostic — while two `settings` blocks in the same position deep-merge.
        // Includes make this shape easy to reach without noticing.
        const string source = """
            settings {
                Db { Host = "h" }
                Db { Port = 5 }
            }
            """;

        var (result, outputDir, tempDir) = Compile(source);

        try
        {
            await Assert.That(result.Success).IsTrue();

            var json = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));

            await Assert.That(json).Contains("Host");
            await Assert.That(json).Contains("Port");
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Test]
    public async Task Compile_ExplicitlyEmptyBlock_IsKeptAsync()
    {
        // An empty object is not invisible at runtime: .NET surfaces it as a key with a
        // null value. Dropping a block the author deliberately wrote empty would change
        // observable configuration.
        const string source = "settings {\n    Features { }\n    A = 1\n}";

        var (result, outputDir, tempDir) = Compile(source);

        try
        {
            await Assert.That(result.Success).IsTrue();

            var json = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));

            await Assert.That(json).Contains("Features");
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Test]
    public async Task Compile_BlockEmptiedByItsConditions_IsStillDroppedAsync()
    {
        // The case the drop exists for: the block overrides nothing, so writing it would
        // claim an override that does not exist — and collide with a primitive of the
        // same name in the base.
        const string source = """
            settings { Foo = "bar" }
            env "Dev" { settings { Foo { X = 1 if false } } }
            """;

        var (result, outputDir, tempDir) = Compile(source);

        try
        {
            await Assert.That(result.Success).IsTrue();

            var overlay = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.Dev.json"));

            await Assert.That(overlay).DoesNotContain("Foo");
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Test]
    public async Task Resolve_MissingInclude_ReportsTheIncludeStatementsLocationAsync()
    {
        // Circular includes were located; not-found and unparsable ones were not, so
        // they landed on the first character of the file.
        using var temp = new TempDir();

        var mainPath = temp.Write("main.settex", "settings { A = 1 }\ninclude \"./absent.settex\"");

        var lexer = new Lexer(await File.ReadAllTextAsync(mainPath), mainPath);
        var ast = new Parser(lexer.Tokenize(), mainPath).Parse();

        var exception = Assert.Throws<IncludeException>(() => new IncludeResolver().ResolveIncludes(ast, mainPath));

        await Assert.That(exception!.Location).IsNotNull();
        await Assert.That(exception.Location!.Line).IsEqualTo(2);
    }

    [Test]
    public async Task Lex_LoneCarriageReturnLineEndings_CountLinesAsync()
    {
        // ScanNewline accepted a lone CR, but only LF advanced the counter — so every
        // diagnostic in a classic-Mac-EOL file reported line 1.
        const string source = "settings {\r    A = undefinedVariable\r}";

        var tokens = new Lexer(source).Tokenize();
        var identifier = tokens.First(t => t.Text == "undefinedVariable");

        await Assert.That(identifier.Location.Line).IsEqualTo(2);
    }

    [Test]
    public async Task Lex_CrLfLineEndings_CountEachLineOnceAsync()
    {
        // The guard: a CRLF pair is one terminator, not two.
        const string source = "settings {\r\n    A = undefinedVariable\r\n}";

        var tokens = new Lexer(source).Tokenize();
        var identifier = tokens.First(t => t.Text == "undefinedVariable");

        await Assert.That(identifier.Location.Line).IsEqualTo(2);
    }

    private static (CompilationResult Result, string OutputDir, string TempDir) Compile(string source)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SettexNestedTests", Guid.NewGuid().ToString());
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

    private sealed class TempDir : IDisposable
    {
        private readonly string path;

        public TempDir()
        {
            this.path = Path.Combine(Path.GetTempPath(), $"settex-inc-{Guid.NewGuid():N}");
            Directory.CreateDirectory(this.path);
        }

        public string Write(string name, string content)
        {
            var full = Path.Combine(this.path, name);
            File.WriteAllText(full, content);
            return full;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.path, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory must never fail a test run.
            }
        }
    }
}
