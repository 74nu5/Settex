namespace Settex.Core.Tests.Compilation;

using System.Text.Json;
using Settex.Compilation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

public class SettexCompilerTests
{
    private string GetTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SettexCompilerTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private void CleanupDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Compile_SimpleSettex_GeneratesAppSettingsJson()
    {
        // Arrange
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var sourceFile = Path.Combine(tempDir, "test.settex");
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(sourceFile, """

                                                 settings {
                                                     ApplicationName = "TestApp"
                                                 }

                                                 """);

        try
        {
            // Act
            var result = compiler.Compile(sourceFile, outputDir);

            // Assert
            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "appsettings.json"))).IsTrue();

            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));
            await Assert.That(content).Contains("ApplicationName");
            await Assert.That(content).Contains("TestApp");
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_WithEnvironments_GeneratesMultipleFiles()
    {
        // Arrange
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var sourceFile = Path.Combine(tempDir, "test.settex");
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(sourceFile, @"
settings {
    ApplicationName = ""TestApp""
    Logging.LogLevel.Default = ""Information""
}

env ""Development"" {
    settings {
        Logging.LogLevel.Default = ""Debug""
    }
}

env ""Production"" {
    settings {
        Logging.LogLevel.Default = ""Warning""
    }
}
");

        try
        {
            // Act
            var result = compiler.Compile(sourceFile, outputDir);

            // Assert
            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "appsettings.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "appsettings.Development.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "appsettings.Production.json"))).IsTrue();

            // Verify base settings
            var baseContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));
            await Assert.That(baseContent).Contains("Information");

            // Verify Development override
            var devContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.Development.json"));
            await Assert.That(devContent).Contains("Debug");

            // Verify Production override
            var prodContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.Production.json"));
            await Assert.That(prodContent).Contains("Warning");
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_NonExistentFile_ReturnsError()
    {
        // Arrange
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var sourceFile = Path.Combine(tempDir, "nonexistent.settex");
        var outputDir = Path.Combine(tempDir, "output");

        try
        {
            // Act
            var result = compiler.Compile(sourceFile, outputDir);

            // Assert
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Count()).IsGreaterThan(0);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_LexerError_ReturnsError()
    {
        // Arrange
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var sourceFile = Path.Combine(tempDir, "test.settex");
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(sourceFile, @"
settings {
    Name = ""Unclosed string
}
");

        try
        {
            // Act
            var result = compiler.Compile(sourceFile, outputDir);

            // Assert
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Count()).IsGreaterThan(0);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_ParserError_ReturnsError()
    {
        // Arrange
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var sourceFile = Path.Combine(tempDir, "test.settex");
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(sourceFile, @"
settings {
    Name = 
}
");

        try
        {
            // Act
            var result = compiler.Compile(sourceFile, outputDir);

            // Assert
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Count()).IsGreaterThan(0);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_EvaluatorError_ReturnsError()
    {
        // Arrange
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var sourceFile = Path.Combine(tempDir, "test.settex");
        var outputDir = Path.Combine(tempDir, "output");

        // No settings block - should trigger evaluator error
        await File.WriteAllTextAsync(sourceFile, @"
env ""Development"" {
    settings {
        Name = ""Test""
    }
}
");

        try
        {
            // Act
            var result = compiler.Compile(sourceFile, outputDir);

            // Assert
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Count()).IsGreaterThan(0);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_ComplexSettings_GeneratesCorrectJson()
    {
        // Arrange
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var sourceFile = Path.Combine(tempDir, "test.settex");
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(sourceFile, @"
settings {
    ApplicationName = ""MyApp""
    Logging.LogLevel.Default = ""Information""
    Logging.LogLevel.Microsoft = ""Warning""
    AllowedHosts = [""localhost"", ""127.0.0.1""]
}
");

        try
        {
            // Act
            var result = compiler.Compile(sourceFile, outputDir);

            // Assert
            await Assert.That(result.Success).IsTrue();

            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));
            var json = JsonDocument.Parse(content);

            await Assert.That(json.RootElement.GetProperty("ApplicationName").GetString()).IsEqualTo("MyApp");
            await Assert.That(json.RootElement.GetProperty("Logging").GetProperty("LogLevel").GetProperty("Default").GetString())
                .IsEqualTo("Information");
            await Assert.That(json.RootElement.GetProperty("AllowedHosts").GetArrayLength()).IsEqualTo(2);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_DiagnosticsContainLocation_ForLexerError()
    {
        // Arrange
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var sourceFile = Path.Combine(tempDir, "test.settex");
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(sourceFile, @"
settings {
    Name = ""Unclosed string
}
");

        try
        {
            // Act
            var result = compiler.Compile(sourceFile, outputDir);

            // Assert
            await Assert.That(result.Success).IsFalse();
            var firstError = result.Errors.First();
            await Assert.That(firstError.Location).IsNotNull();
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }
}
