namespace Settex.Cli.Tests;

using Spectre.Console;

using TUnit.Core;

/// <summary>
/// Black-box tests for the Settex CLI: they invoke <see cref="Program.Main"/> in
/// process and assert on the exit code and the rendered output a user would see.
/// The Spectre and System.Console outputs are redirected per test, so the class
/// runs sequentially to avoid races on that global console state.
/// </summary>
[NotInParallel]
public class CliTests
{
    [Test]
    public async Task Build_ValidFile_ReturnsZeroAndWritesJson()
    {
        using var workspace = new TempWorkspace();
        var source = workspace.WriteSource(
            """
            settings {
                Server.Port = 8080
            }
            """);
        var outputDir = workspace.OutputDirectory;

        var (exitCode, output) = await RunAsync("build", source, "-o", outputDir);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Successfully generated");

        var generated = Path.Combine(outputDir, "appsettings.json");
        await Assert.That(File.Exists(generated)).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(generated)).Contains("8080");
    }

    [Test]
    public async Task Build_MissingFile_ReturnsOneAndReportsNotFound()
    {
        using var workspace = new TempWorkspace();
        var missing = Path.Combine(workspace.Root, "does-not-exist.settex");

        var (exitCode, output) = await RunAsync("build", missing);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("File not found");
    }

    [Test]
    public async Task Build_SyntaxError_ReturnsOneAndReportsFailure()
    {
        using var workspace = new TempWorkspace();
        // Unterminated string -> a lexer diagnostic, not a crash.
        var source = workspace.WriteSource("settings {\n    X = \"oops\n}");

        var (exitCode, output) = await RunAsync("build", source, "-o", workspace.OutputDirectory);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Compilation failed");
    }

    [Test]
    public async Task Build_CoverageDrift_WarnsButSucceeds()
    {
        using var workspace = new TempWorkspace();
        var source = workspace.WriteSource(
            """
            settings { ApplicationName = "MyApp" }

            env "Development" {
                settings { DevOnly.Flag = true }
            }

            env "Production" {
                settings { Logging.LogLevel.Default = "Warning" }
            }
            """);

        var (exitCode, output) = await RunAsync("build", source, "-o", workspace.OutputDirectory);

        // Drift is advisory: the build still succeeds but surfaces the warning.
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("DevOnly.Flag");

        // Default output is delta: the environment file omits the base key.
        var dev = await File.ReadAllTextAsync(Path.Combine(workspace.OutputDirectory, "appsettings.Development.json"));
        await Assert.That(dev).DoesNotContain("ApplicationName");
    }

    [Test]
    public async Task Build_MergedFlag_IncludesBaseKeyInEnvironmentFile()
    {
        using var workspace = new TempWorkspace();
        var source = workspace.WriteSource(
            """
            settings { ApplicationName = "MyApp" }

            env "Development" {
                settings { Logging.LogLevel.Default = "Debug" }
            }
            """);

        var (exitCode, _) = await RunAsync("build", source, "-o", workspace.OutputDirectory, "--merged");

        await Assert.That(exitCode).IsEqualTo(0);

        var dev = await File.ReadAllTextAsync(Path.Combine(workspace.OutputDirectory, "appsettings.Development.json"));
        await Assert.That(dev).Contains("ApplicationName");
        await Assert.That(dev).Contains("Debug");
    }

    [Test]
    public async Task Build_WithoutFileArgument_ReturnsNonZero()
    {
        var (exitCode, _) = await RunAsync("build");

        await Assert.That(exitCode).IsNotEqualTo(0);
    }

    [Test]
    public async Task Help_ReturnsZero()
    {
        // Help text is emitted by System.CommandLine (not captured here); the
        // contract under test is the zero exit code.
        var (exitCode, _) = await RunAsync("--help");

        await Assert.That(exitCode).IsEqualTo(0);
    }

    /// <summary>
    /// Runs the CLI in process with the given arguments and captures the Spectre
    /// output — which is where the CLI writes all of its own messages (success,
    /// failure, "file not found", diagnostics). Argument-parsing output produced by
    /// System.CommandLine itself is not redirected here (that would require
    /// overwriting the Console writer, which the test framework forbids); those
    /// cases are asserted on the exit code only.
    /// </summary>
    private static async Task<(int ExitCode, string Output)> RunAsync(params string[] args)
    {
        var writer = new StringWriter();
        var previousConsole = AnsiConsole.Console;

        try
        {
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.No,
                Out = new AnsiConsoleOutput(writer),
            });

            var exitCode = await Program.Main(args);
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
        }
    }

    /// <summary>
    /// A throwaway working directory with helpers to write a source file and hold
    /// an output directory, cleaned up on dispose.
    /// </summary>
    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            this.Root = Path.Combine(Path.GetTempPath(), "settex-cli-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(this.Root);
            this.OutputDirectory = Path.Combine(this.Root, "out");
        }

        public string Root { get; }

        public string OutputDirectory { get; }

        public string WriteSource(string content)
        {
            var path = Path.Combine(this.Root, "appsettings.settex");
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.Root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
