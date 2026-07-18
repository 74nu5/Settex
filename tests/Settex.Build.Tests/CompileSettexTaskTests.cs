namespace Settex.Build.Tests;

using Microsoft.Build.Framework;

using Settex.Build;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

/// <summary>
/// End-to-end tests for the MSBuild task that compiles .settex files during a build.
/// </summary>
public class CompileSettexTaskTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SettexBuildTaskTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Test]
    public async Task Execute_ValidSettex_ReturnsTrueAndGeneratesFiles()
    {
        var dir = CreateTempDir();
        try
        {
            var source = Path.Combine(dir, "appsettings.settex");
            var output = Path.Combine(dir, "out");
            await File.WriteAllTextAsync(source, """
                settings {
                  ApplicationName = "MyApp"
                }

                env "Development" {
                  settings {
                    ApplicationName = "MyApp.Dev"
                  }
                }
                """);

            var engine = new FakeBuildEngine();
            var task = new CompileSettexTask
            {
                BuildEngine = engine,
                SourceFile = source,
                OutputDirectory = output,
            };

            var result = task.Execute();

            await Assert.That(result).IsTrue();
            await Assert.That(engine.Errors).IsEmpty();
            await Assert.That(task.GeneratedFiles.Length).IsGreaterThanOrEqualTo(2);
            await Assert.That(File.Exists(Path.Combine(output, "appsettings.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(output, "appsettings.Development.json"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Execute_InvalidSettex_ReturnsFalseAndLogsError()
    {
        var dir = CreateTempDir();
        try
        {
            var source = Path.Combine(dir, "appsettings.settex");
            await File.WriteAllTextAsync(source, """
                settings {
                  Name = "unterminated
                }
                """);

            var engine = new FakeBuildEngine();
            var task = new CompileSettexTask
            {
                BuildEngine = engine,
                SourceFile = source,
                OutputDirectory = Path.Combine(dir, "out"),
            };

            var result = task.Execute();

            await Assert.That(result).IsFalse();
            await Assert.That(engine.Errors).IsNotEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Execute_MissingSourceFile_ReturnsFalse()
    {
        var dir = CreateTempDir();
        try
        {
            var engine = new FakeBuildEngine();
            var task = new CompileSettexTask
            {
                BuildEngine = engine,
                SourceFile = Path.Combine(dir, "does-not-exist.settex"),
                OutputDirectory = Path.Combine(dir, "out"),
            };

            var result = task.Execute();

            await Assert.That(result).IsFalse();
            await Assert.That(engine.Errors).IsNotEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Execute_ErrorDiagnostics_CarryTheSettexErrorCode()
    {
        var dir = CreateTempDir();
        try
        {
            var source = Path.Combine(dir, "appsettings.settex");
            await File.WriteAllTextAsync(source, "settings { Name = @@ }");

            var engine = new FakeBuildEngine();
            var task = new CompileSettexTask
            {
                BuildEngine = engine,
                SourceFile = source,
                OutputDirectory = Path.Combine(dir, "out"),
            };

            task.Execute();

            await Assert.That(engine.Errors.Any(e => e.Code == "SETTEX")).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Minimal IBuildEngine that records what the task logs.
    /// </summary>
    private sealed class FakeBuildEngine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = new();
        public List<BuildWarningEventArgs> Warnings { get; } = new();
        public List<BuildMessageEventArgs> Messages { get; } = new();

        public void LogErrorEvent(BuildErrorEventArgs e) => this.Errors.Add(e);
        public void LogWarningEvent(BuildWarningEventArgs e) => this.Warnings.Add(e);
        public void LogMessageEvent(BuildMessageEventArgs e) => this.Messages.Add(e);
        public void LogCustomEvent(CustomBuildEventArgs e) { }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs) => false;

        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => string.Empty;
    }
}
