namespace Settex.Core.Tests.Writing;

using System.Text.Json.Nodes;

using Settex.Core.Evaluation;
using Settex.Core.Writing;

public sealed class JsonWriterTests
{
    [Test]
    public async Task WriteSettings_EmptyBase_WritesEmptyJson()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel(
            [],
            []);

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act
            writer.WriteSettings(model, outputDir);

            // Assert
            var baseFile = Path.Combine(outputDir, "appsettings.json");
            await Assert.That(File.Exists(baseFile)).IsTrue();

            var content = await File.ReadAllTextAsync(baseFile);
            await Assert.That(content).Contains("{}");
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    [Test]
    public async Task WriteSettings_SimpleBase_WritesCorrectJson()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel(new()
            {
                ["ApplicationName"] = "TestApp",
            },
            []
        );

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act
            writer.WriteSettings(model, outputDir);

            // Assert
            var baseFile = Path.Combine(outputDir, "appsettings.json");
            await Assert.That(File.Exists(baseFile)).IsTrue();

            var content = await File.ReadAllTextAsync(baseFile);
            await Assert.That(content).Contains("ApplicationName");
            await Assert.That(content).Contains("TestApp");
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    [Test]
    public async Task WriteSettings_WithEnvironment_WritesBothFiles()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel(new()
            {
                ["ApplicationName"] = "TestApp",
            },
            new()
            {
                ["Development"] = new()
                {
                    ["Logging"] = new JsonObject
                    {
                        ["LogLevel"] = new JsonObject
                        {
                            ["Default"] = "Debug",
                        },
                    },
                },
            }
        );

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act
            writer.WriteSettings(model, outputDir);

            // Assert
            var baseFile = Path.Combine(outputDir, "appsettings.json");
            var devFile = Path.Combine(outputDir, "appsettings.Development.json");

            await Assert.That(File.Exists(baseFile)).IsTrue();
            await Assert.That(File.Exists(devFile)).IsTrue();

            var devContent = await File.ReadAllTextAsync(devFile);
            await Assert.That(devContent).Contains("ApplicationName");
            await Assert.That(devContent).Contains("Debug");
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    [Test]
    public async Task WriteSettings_ConditionalWrite_SkipsUnchangedFiles()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel(new()
            {
                ["ApplicationName"] = "TestApp",
            },
            []
        );

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act - first write
            writer.WriteSettings(model, outputDir);
            var baseFile = Path.Combine(outputDir, "appsettings.json");
            var firstWriteTime = File.GetLastWriteTimeUtc(baseFile);

            // Wait a bit to ensure timestamp would differ if file was rewritten
            await Task.Delay(100);

            // Act - second write with same content
            writer.WriteSettings(model, outputDir);
            var secondWriteTime = File.GetLastWriteTimeUtc(baseFile);

            // Assert - file should not have been rewritten
            await Assert.That(secondWriteTime).IsEqualTo(firstWriteTime);
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    [Test]
    public async Task WriteSettings_InvalidEnvironmentName_ThrowsException()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel([],
            new()
            {
                ["Dev<>ment"] = [], // Invalid characters
            });

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<JsonWriterException>(async () =>
            {
                writer.WriteSettings(model, outputDir);
                await Task.CompletedTask;
            });
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    [Test]
    public async Task WriteSettings_EmptyEnvironmentName_ThrowsException()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel([],
            new()
            {
                [""] = [],
            });

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<JsonWriterException>(async () =>
            {
                writer.WriteSettings(model, outputDir);
                await Task.CompletedTask;
            });
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    [Test]
    public async Task WriteSettings_CreatesOutputDirectory()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel([],
            []
        );

        var outputDir = Path.Combine(Path.GetTempPath(), "SettexTests", Guid.NewGuid().ToString(), "nested", "path");

        try
        {
            // Act
            writer.WriteSettings(model, outputDir);

            // Assert
            await Assert.That(Directory.Exists(outputDir)).IsTrue();
        }
        finally
        {
            var rootDir = Path.Combine(Path.GetTempPath(), "SettexTests");
            this.CleanupDirectory(rootDir);
        }
    }

    [Test]
    public async Task WriteSettings_MultipleEnvironments_WritesAllFiles()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel(new()
            {
                ["ApplicationName"] = "TestApp",
            },
            new()
            {
                ["Development"] = new() { ["Env"] = "Dev" },
                ["Staging"] = new() { ["Env"] = "Stg" },
                ["Production"] = new() { ["Env"] = "Prod" },
            });

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act
            writer.WriteSettings(model, outputDir);

            // Assert
            await Assert.That(File.Exists(Path.Combine(outputDir, "appsettings.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "appsettings.Development.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "appsettings.Staging.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "appsettings.Production.json"))).IsTrue();
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    [Test]
    public async Task WriteSettings_ComplexNested_PreservesStructure()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel(new()
            {
                ["Logging"] = new JsonObject
                {
                    ["LogLevel"] = new JsonObject
                    {
                        ["Default"] = "Information",
                        ["Microsoft"] = "Warning",
                    },
                },
            },
            []);

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act
            writer.WriteSettings(model, outputDir);

            // Assert
            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));
            await Assert.That(content).Contains("Logging");
            await Assert.That(content).Contains("LogLevel");
            await Assert.That(content).Contains("Information");
            await Assert.That(content).Contains("Warning");
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    [Test]
    public async Task WriteSettings_Arrays_SerializesCorrectly()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel(new()
            {
                ["AllowedHosts"] = new JsonArray("localhost", "127.0.0.1"),
            },
            []
        );

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act
            writer.WriteSettings(model, outputDir);

            // Assert
            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));
            await Assert.That(content).Contains("AllowedHosts");
            await Assert.That(content).Contains("localhost");
            await Assert.That(content).Contains("127.0.0.1");
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    [Test]
    public async Task WriteSettings_DifferentContent_RewritesFile()
    {
        // Arrange
        var writer = new JsonWriter();
        var model1 = new SettingsModel(new() { ["Version"] = 1 },
            []
        );

        var model2 = new SettingsModel(new() { ["Version"] = 2 },
            []
        );

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act - first write
            writer.WriteSettings(model1, outputDir);
            var firstContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));

            // Act - second write with different content
            writer.WriteSettings(model2, outputDir);
            var secondContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));

            // Assert - content should differ
            await Assert.That(secondContent).IsNotEqualTo(firstContent);
            await Assert.That(secondContent).Contains("\"Version\": 2");
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    [Test]
    public async Task WriteSettings_TrailingNewline_AddsConsistently()
    {
        // Arrange
        var writer = new JsonWriter();
        var model = new SettingsModel(new() { ["Test"] = "Value" }, []);

        var outputDir = this.GetTempDirectory();

        try
        {
            // Act
            writer.WriteSettings(model, outputDir);

            // Assert
            var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json"));
            await Assert.That(content.EndsWith("\n")).IsTrue();
        }
        finally
        {
            this.CleanupDirectory(outputDir);
        }
    }

    private string GetTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SettexTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private void CleanupDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
