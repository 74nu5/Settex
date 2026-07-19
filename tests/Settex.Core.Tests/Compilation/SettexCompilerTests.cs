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

    /// <summary>
    /// Compiles a source string through the full compiler and returns the parsed
    /// content of a generated output file. Throws if compilation fails.
    /// </summary>
    private static async Task<JsonElement> CompileAndReadAsync(string tempDir, string source, string outputFileName, CompilerOptions? options = null)
    {
        var sourceFile = Path.Combine(tempDir, "appsettings.settex");
        var outputDir = Path.Combine(tempDir, "output");
        await File.WriteAllTextAsync(sourceFile, source);

        var result = new SettexCompiler().Compile(sourceFile, outputDir, options);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                "Compilation failed: " + string.Join("; ", result.Errors.Select(e => e.Message)));
        }

        var json = await File.ReadAllTextAsync(Path.Combine(outputDir, outputFileName));

        // Clone so the JsonElement stays valid after the JsonDocument is disposed.
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
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

    [Test]
    public async Task Compile_IncludedFileWithSettingsBlock_MergesIntoBase()
    {
        // An included file that carries its own settings block is deep-merged
        // into the including file's base settings (modular configuration).
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(Path.Combine(tempDir, "logging.settex"), """
                                                                              settings {
                                                                                Logging { LogLevel { Default = "Information" } }
                                                                              }
                                                                              """);

        var sourceFile = Path.Combine(tempDir, "appsettings.settex");
        await File.WriteAllTextAsync(sourceFile, """
                                                 include "./logging.settex"

                                                 settings {
                                                   ApplicationName = "MyApp"
                                                 }
                                                 """);

        try
        {
            var result = compiler.Compile(sourceFile, outputDir);

            await Assert.That(result.Success).IsTrue();

            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json")));
            var root = doc.RootElement;
            await Assert.That(root.GetProperty("ApplicationName").GetString()).IsEqualTo("MyApp");
            await Assert.That(root.GetProperty("Logging").GetProperty("LogLevel").GetProperty("Default").GetString())
                        .IsEqualTo("Information");
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_IncludedSettingsBlock_MainOverridesInclude()
    {
        // On a key conflict the including file (later in document order) wins.
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(Path.Combine(tempDir, "defaults.settex"), """
                                                                               settings {
                                                                                 Server { Host = "localhost" Port = 8080 }
                                                                               }
                                                                               """);

        var sourceFile = Path.Combine(tempDir, "appsettings.settex");
        await File.WriteAllTextAsync(sourceFile, """
                                                 include "./defaults.settex"

                                                 settings {
                                                   Server { Port = 9090 }
                                                 }
                                                 """);

        try
        {
            var result = compiler.Compile(sourceFile, outputDir);

            await Assert.That(result.Success).IsTrue();

            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json")));
            var server = doc.RootElement.GetProperty("Server");
            await Assert.That(server.GetProperty("Host").GetString()).IsEqualTo("localhost");
            await Assert.That(server.GetProperty("Port").GetInt64()).IsEqualTo(9090L);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_IncludedFileContributesEnvOverlay_Merges()
    {
        // An included file can contribute an env block; overlays for the same
        // environment merge across files.
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(Path.Combine(tempDir, "prod.settex"), """
                                                                           env "Production" {
                                                                             settings {
                                                                               Server { Ssl = true }
                                                                             }
                                                                           }
                                                                           """);

        var sourceFile = Path.Combine(tempDir, "appsettings.settex");
        await File.WriteAllTextAsync(sourceFile, """
                                                 include "./prod.settex"

                                                 settings {
                                                   Server { Host = "localhost" }
                                                 }

                                                 env "Production" {
                                                   settings {
                                                     Server { Host = "prod-host" }
                                                   }
                                                 }
                                                 """);

        try
        {
            var result = compiler.Compile(sourceFile, outputDir);

            await Assert.That(result.Success).IsTrue();

            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.Production.json")));
            var server = doc.RootElement.GetProperty("Server");
            await Assert.That(server.GetProperty("Host").GetString()).IsEqualTo("prod-host");
            await Assert.That(server.GetProperty("Ssl").GetBoolean()).IsEqualTo(true);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_VariablesAndInterpolation_ResolvedInOutput()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            var root = await CompileAndReadAsync(tempDir, """
                let host = "localhost"
                let port = 8000
                let protocol = "https"

                settings {
                  BaseUrl = "${protocol}://${host}:${port}"
                  Message = "Port is ${port + 100}"
                }
                """, "appsettings.json");

            await Assert.That(root.GetProperty("BaseUrl").GetString()).IsEqualTo("https://localhost:8000");
            await Assert.That(root.GetProperty("Message").GetString()).IsEqualTo("Port is 8100");
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_Expressions_EvaluatedInOutput()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            var root = await CompileAndReadAsync(tempDir, """
                let timeout = 30
                let retries = 3
                let enabled = true

                settings {
                  Total = timeout * retries
                  Sum = 8000 + 80
                  ShouldCache = enabled and timeout > 10
                  Not = not enabled
                  LogLevel = null ?? "Information"
                  IsBig = timeout >= 30
                }
                """, "appsettings.json");

            await Assert.That(root.GetProperty("Total").GetInt64()).IsEqualTo(90L);
            await Assert.That(root.GetProperty("Sum").GetInt64()).IsEqualTo(8080L);
            await Assert.That(root.GetProperty("ShouldCache").GetBoolean()).IsEqualTo(true);
            await Assert.That(root.GetProperty("Not").GetBoolean()).IsEqualTo(false);
            await Assert.That(root.GetProperty("LogLevel").GetString()).IsEqualTo("Information");
            await Assert.That(root.GetProperty("IsBig").GetBoolean()).IsEqualTo(true);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_ForLoop_GeneratesArrayElements()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            var root = await CompileAndReadAsync(tempDir, """
                let services = [
                  svc { Name = "auth" Port = 8001 }
                  svc { Name = "api"  Port = 8002 }
                ]
                let host = "localhost"

                settings {
                  Endpoints = [
                    for s in services {
                      item {
                        Name = s.Name
                        Url = "http://${host}:${s.Port}"
                      }
                    }
                  ]
                }
                """, "appsettings.json");

            var endpoints = root.GetProperty("Endpoints");
            await Assert.That(endpoints.GetArrayLength()).IsEqualTo(2);
            await Assert.That(endpoints[0].GetProperty("Name").GetString()).IsEqualTo("auth");
            await Assert.That(endpoints[0].GetProperty("Url").GetString()).IsEqualTo("http://localhost:8001");
            await Assert.That(endpoints[1].GetProperty("Url").GetString()).IsEqualTo("http://localhost:8002");
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_ConditionalInsideEnv_AppliesWhenTrue()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            const string source = """
                settings {
                  LogLevel = "Warning"
                }

                env "Development" {
                  let verbose = true
                  settings {
                    LogLevel = "Debug" if verbose
                  }
                }
                """;

            var baseRoot = await CompileAndReadAsync(tempDir, source, "appsettings.json");
            await Assert.That(baseRoot.GetProperty("LogLevel").GetString()).IsEqualTo("Warning");

            var devRoot = await CompileAndReadAsync(tempDir, source, "appsettings.Development.json");
            await Assert.That(devRoot.GetProperty("LogLevel").GetString()).IsEqualTo("Debug");
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_SetIfMissing_KeepsBaseDefaultInOverlay()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            const string source = """
                settings {
                  Server { Port := 8080 }
                }

                env "Production" {
                  settings {
                    Server { Port := 443 }
                  }
                }
                """;

            // Merged output so the effective per-environment config can be asserted.
            var merged = new CompilerOptions { MergeEnvironments = true };

            var baseRoot = await CompileAndReadAsync(tempDir, source, "appsettings.json", merged);
            await Assert.That(baseRoot.GetProperty("Server").GetProperty("Port").GetInt64()).IsEqualTo(8080L);

            // := in the overlay must not override the value already set in base.
            var prodRoot = await CompileAndReadAsync(tempDir, source, "appsettings.Production.json", merged);
            await Assert.That(prodRoot.GetProperty("Server").GetProperty("Port").GetInt64()).IsEqualTo(8080L);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_EnvOnlyKeyMissingElsewhere_WarnsButSucceeds()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            const string source = """
                settings { ApplicationName = "MyApp" }

                env "Development" {
                  settings { DevOnly.Flag = true }
                }

                env "Production" {
                  settings { Logging.LogLevel.Default = "Warning" }
                }
                """;

            var sourceFile = Path.Combine(tempDir, "appsettings.settex");
            var outputDir = Path.Combine(tempDir, "output");
            await File.WriteAllTextAsync(sourceFile, source);

            var result = new SettexCompiler().Compile(sourceFile, outputDir);

            // Coverage drift is advisory: a warning, not a failure.
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Warnings.Any(w => w.Message.Contains("DevOnly.Flag"))).IsTrue();

            // Default output is delta: the environment file omits the base key.
            var devContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.Development.json"));
            await Assert.That(devContent).DoesNotContain("ApplicationName");

            // The check can be turned off.
            var quiet = new SettexCompiler().Compile(
                sourceFile, outputDir, new CompilerOptions { CheckCoverage = false });
            await Assert.That(quiet.Warnings.Any(w => w.Message.Contains("DevOnly.Flag"))).IsFalse();
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_EnvShortensBaseArray_WarnsButSucceeds()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            const string source = """
                settings { AllowedHosts = ["a", "b", "c"] }
                env "Production" { settings { AllowedHosts = ["x"] } }
                """;

            var sourceFile = Path.Combine(tempDir, "appsettings.settex");
            var outputDir = Path.Combine(tempDir, "output");
            await File.WriteAllTextAsync(sourceFile, source);

            var result = new SettexCompiler().Compile(sourceFile, outputDir);

            // Advisory: the array-layering trap is a warning, not a failure.
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Warnings.Any(w => w.Message.Contains("AllowedHosts") && w.Message.Contains("index"))).IsTrue();

            // Silenced together with the coverage check.
            var quiet = new SettexCompiler().Compile(sourceFile, outputDir, new CompilerOptions { CheckCoverage = false });
            await Assert.That(quiet.Warnings.Any(w => w.Message.Contains("AllowedHosts"))).IsFalse();
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_BaseOverlayTypeConflict_RejectedInBothOutputModes()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            // Base says Foo is a number, the overlay makes it an object. Delta output
            // never merges base and overlay, so this used to slip through and emit an
            // incoherent pair of files; it must be rejected regardless of mode.
            const string source = """
                settings { Foo = 1 }
                env "Dev" { settings { Foo { Bar = 2 } } }
                """;

            var sourceFile = Path.Combine(tempDir, "appsettings.settex");
            var outputDir = Path.Combine(tempDir, "output");
            await File.WriteAllTextAsync(sourceFile, source);

            var delta = new SettexCompiler().Compile(sourceFile, outputDir);
            await Assert.That(delta.Success).IsFalse();

            var deltaError = delta.Errors.FirstOrDefault(e => e.Message.Contains("Type mismatch"));
            await Assert.That(deltaError).IsNotNull();
            await Assert.That(deltaError!.Message).Contains("Dev");
            await Assert.That(deltaError.Location).IsNotNull();

            // Merged output rejects it too — the two modes agree.
            var merged = new SettexCompiler().Compile(
                sourceFile, outputDir, new CompilerOptions { MergeEnvironments = true });
            await Assert.That(merged.Success).IsFalse();
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_NestedBaseOverlayConflict_NamesFullPath()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            const string source = """
                settings { Server { Port = 1 } }
                env "Dev" { settings { Server { Port { Deep = 2 } } } }
                """;

            var sourceFile = Path.Combine(tempDir, "appsettings.settex");
            var outputDir = Path.Combine(tempDir, "output");
            await File.WriteAllTextAsync(sourceFile, source);

            var result = new SettexCompiler().Compile(sourceFile, outputDir);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(e => e.Message.Contains("Server.Port"))).IsTrue();
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_CompatibleOverlayTypes_StillCompile()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            // Objects deep-merge, arrays replace, primitives replace — none of these
            // are conflicts and must keep compiling.
            const string source = """
                settings {
                  Server { Host = "localhost" Port = 5432 }
                  Tags = ["dev"]
                  Name = "app"
                }

                env "Dev" {
                  settings {
                    Server { Port = 6000 }
                    Tags = ["a", "b"]
                    Name = "app-dev"
                    NewKey = true
                  }
                }
                """;

            var sourceFile = Path.Combine(tempDir, "appsettings.settex");
            var outputDir = Path.Combine(tempDir, "output");
            await File.WriteAllTextAsync(sourceFile, source);

            var result = new SettexCompiler().Compile(sourceFile, outputDir);

            await Assert.That(result.Success).IsTrue();
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_TypeMismatchBetweenBlocks_ReportsLocatedError()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            // Two base blocks disagree on the shape of 'Foo' (common via includes).
            const string source = """
                settings { Foo = 1 }
                settings { Foo { Bar = 2 } }
                """;

            var sourceFile = Path.Combine(tempDir, "appsettings.settex");
            var outputDir = Path.Combine(tempDir, "output");
            await File.WriteAllTextAsync(sourceFile, source);

            var result = new SettexCompiler().Compile(sourceFile, outputDir);

            await Assert.That(result.Success).IsFalse();

            var error = result.Errors.FirstOrDefault(e => e.Message.Contains("Type mismatch"));
            await Assert.That(error).IsNotNull();
            // A located diagnostic, not an unlocated "Unexpected error".
            await Assert.That(error!.Location).IsNotNull();
            await Assert.That(error.Message).DoesNotContain("Unexpected error");
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_EnvOverlay_DeepMergesBaseAndOverlay()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            // Merged output so the effective per-environment config can be asserted.
            var prodRoot = await CompileAndReadAsync(tempDir, """
                settings {
                  Database { Host = "localhost" Port = 5432 }
                  Tags = ["dev", "test"]
                }

                env "Production" {
                  settings {
                    Database.Host = "prod-server"
                    Tags = ["prod"]
                  }
                }
                """, "appsettings.Production.json", new CompilerOptions { MergeEnvironments = true });

            var db = prodRoot.GetProperty("Database");
            await Assert.That(db.GetProperty("Host").GetString()).IsEqualTo("prod-server");
            await Assert.That(db.GetProperty("Port").GetInt64()).IsEqualTo(5432L); // preserved from base

            var tags = prodRoot.GetProperty("Tags");
            await Assert.That(tags.GetArrayLength()).IsEqualTo(1); // array replaced, not merged
            await Assert.That(tags[0].GetString()).IsEqualTo("prod");
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_KitchenSink_AllV2FeaturesTogether()
    {
        // One file exercising includes + let + expressions + interpolation + for +
        // conditionals + set-if-missing + env overlays through the full compiler.
        var tempDir = this.GetTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "common.settex"), """
                let baseHost = "localhost"
                let basePort = 8000
                let services = [
                  svc { Name = "auth" Port = 8001 }
                  svc { Name = "api"  Port = 8002 }
                ]

                settings {
                  Server { Host := baseHost Port := basePort }
                }
                """);

            const string source = """
                include "./common.settex"

                settings {
                  ApplicationName = "MyApp"
                  BaseUrl = "http://${baseHost}:${basePort}"
                  MaxConnections = 10 * 10
                  Endpoints = [
                    for s in services {
                      item { Name = s.Name Url = "http://${baseHost}:${s.Port}" }
                    }
                  ]
                }

                env "Production" {
                  let secure = true
                  settings {
                    Server.Port = 443
                    Security.RequireHttps = true if secure
                  }
                }
                """;

            var baseRoot = await CompileAndReadAsync(tempDir, source, "appsettings.json");
            await Assert.That(baseRoot.GetProperty("ApplicationName").GetString()).IsEqualTo("MyApp");
            await Assert.That(baseRoot.GetProperty("BaseUrl").GetString()).IsEqualTo("http://localhost:8000");
            await Assert.That(baseRoot.GetProperty("MaxConnections").GetInt64()).IsEqualTo(100L);
            await Assert.That(baseRoot.GetProperty("Server").GetProperty("Port").GetInt64()).IsEqualTo(8000L);
            await Assert.That(baseRoot.GetProperty("Endpoints").GetArrayLength()).IsEqualTo(2);

            var prodRoot = await CompileAndReadAsync(tempDir, source, "appsettings.Production.json");
            await Assert.That(prodRoot.GetProperty("Server").GetProperty("Port").GetInt64()).IsEqualTo(443L);
            await Assert.That(prodRoot.GetProperty("Security").GetProperty("RequireHttps").GetBoolean()).IsEqualTo(true);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }

    [Test]
    public async Task Compile_ModularConfig_LetsInIncludeAndSettingsInMain()
    {
        // Realistic modular setup: shared variables in one file, settings split
        // across an included module and the main file.
        var compiler = new SettexCompiler();
        var tempDir = this.GetTempDirectory();
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(Path.Combine(tempDir, "common.settex"), """
                                                                             let host = "localhost"
                                                                             let basePort = 8000

                                                                             settings {
                                                                               Server { Host = host Port = basePort }
                                                                             }
                                                                             """);

        var sourceFile = Path.Combine(tempDir, "appsettings.settex");
        await File.WriteAllTextAsync(sourceFile, """
                                                 include "./common.settex"

                                                 settings {
                                                   ApplicationName = "MyApp"
                                                   BaseUrl = "http://${host}:${basePort}"
                                                 }
                                                 """);

        try
        {
            var result = compiler.Compile(sourceFile, outputDir);

            await Assert.That(result.Success).IsTrue();

            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDir, "appsettings.json")));
            var root = doc.RootElement;
            await Assert.That(root.GetProperty("ApplicationName").GetString()).IsEqualTo("MyApp");
            await Assert.That(root.GetProperty("BaseUrl").GetString()).IsEqualTo("http://localhost:8000");
            var server = root.GetProperty("Server");
            await Assert.That(server.GetProperty("Host").GetString()).IsEqualTo("localhost");
            await Assert.That(server.GetProperty("Port").GetInt64()).IsEqualTo(8000L);
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }
    [Test]
    public async Task Compile_BaseOverlayTypeConflictDifferingOnlyInCase_IsRejected()
    {
        var tempDir = this.GetTempDirectory();
        try
        {
            // Same conflict as above, but the overlay spells the key 'foo'. .NET
            // configuration keys are case-insensitive, so the two collide at runtime
            // exactly as 'Foo'/'Foo' would. Looking the base key up ordinally made this
            // the one variant that compiled cleanly — the conflict most worth catching
            // was the one that slipped through.
            const string source = """
                settings { Foo = 1 }
                env "Dev" { settings { foo { Bar = 2 } } }
                """;

            var sourceFile = Path.Combine(tempDir, "appsettings.settex");
            var outputDir = Path.Combine(tempDir, "output");
            await File.WriteAllTextAsync(sourceFile, source);

            var result = new SettexCompiler().Compile(sourceFile, outputDir);

            await Assert.That(result.Success).IsFalse();

            var error = result.Errors.FirstOrDefault(e => e.Message.Contains("Type mismatch"));

            await Assert.That(error).IsNotNull();
            await Assert.That(error!.Message).Contains("foo");
        }
        finally
        {
            this.CleanupDirectory(tempDir);
        }
    }
}
