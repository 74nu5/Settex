namespace Settex.Core.Tests.Evaluation;

using System.Text.Json.Nodes;

using Settex.Core.Evaluation;
using Settex.Core.Lexer;
using Settex.Core.Parser;

public sealed class EvaluatorTests
{
    [Test]
    public async Task Evaluate_EmptySettings_ReturnsEmptyObject()
    {
        // Arrange
        var source = "settings { }";

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        await Assert.That(model.BaseSettings.Count).IsEqualTo(0);
        await Assert.That(model.EnvironmentOverlays).HasCount().EqualTo(0);
    }

    [Test]
    public async Task Evaluate_SimpleAssignment_CreatesProperty()
    {
        // Arrange
        var source = """
                     settings {
                       ApplicationName = "Shop"
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        await Assert.That(model.BaseSettings["ApplicationName"]!.GetValue<string>()).IsEqualTo("Shop");
    }

    [Test]
    public async Task Evaluate_DotPathAssignment_CreatesNestedObjects()
    {
        // Arrange
        var source = """
                     settings {
                       Logging.LogLevel.Default = "Debug"
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        var logging = model.BaseSettings["Logging"] as JsonObject;
        await Assert.That(logging).IsNotNull();

        var logLevel = logging!["LogLevel"] as JsonObject;
        await Assert.That(logLevel).IsNotNull();

        await Assert.That(logLevel!["Default"]!.GetValue<string>()).IsEqualTo("Debug");
    }

    [Test]
    public async Task Evaluate_NestedBlock_CreatesObject()
    {
        // Arrange
        var source = """
                     settings {
                       Server {
                         Host = "localhost"
                         Port = 8080
                       }
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        var server = model.BaseSettings["Server"] as JsonObject;
        await Assert.That(server).IsNotNull();
        await Assert.That(server!["Host"]!.GetValue<string>()).IsEqualTo("localhost");
        await Assert.That(server["Port"]!.GetValue<long>()).IsEqualTo(8080L);
    }

    [Test]
    public async Task Evaluate_Array_CreatesJsonArray()
    {
        // Arrange
        var source = """
                     settings {
                       AllowedHosts = ["localhost", "example.com"]
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        var array = model.BaseSettings["AllowedHosts"] as JsonArray;
        await Assert.That(array).IsNotNull();
        await Assert.That(array).HasCount().EqualTo(2);
        await Assert.That(array![0]!.GetValue<string>()).IsEqualTo("localhost");
        await Assert.That(array[1]!.GetValue<string>()).IsEqualTo("example.com");
    }

    [Test]
    public async Task Evaluate_ArrayWithTaggedObjects_CreatesArrayOfObjects()
    {
        // Arrange
        var source = """
                     settings {
                       Services = [
                         service {
                           Name = "API"
                           Port = 5000
                         }
                         service {
                           Name = "Web"
                           Port = 3000
                         }
                       ]
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        var array = model.BaseSettings["Services"] as JsonArray;
        await Assert.That(array).HasCount().EqualTo(2);

        var service1 = array![0] as JsonObject;
        await Assert.That(service1!["Name"]!.GetValue<string>()).IsEqualTo("API");
        await Assert.That(service1["Port"]!.GetValue<long>()).IsEqualTo(5000L);

        var service2 = array[1] as JsonObject;
        await Assert.That(service2!["Name"]!.GetValue<string>()).IsEqualTo("Web");
        await Assert.That(service2["Port"]!.GetValue<long>()).IsEqualTo(3000L);
    }

    [Test]
    public async Task Evaluate_TaggedObjectAsValue_CreatesObject()
    {
        // Arrange
        var source = """
                     settings {
                       Database = connection {
                         Host = "localhost"
                         Port = 5432
                       }
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        var database = model.BaseSettings["Database"] as JsonObject;
        await Assert.That(database).IsNotNull();
        await Assert.That(database!["Host"]!.GetValue<string>()).IsEqualTo("localhost");
        await Assert.That(database["Port"]!.GetValue<long>()).IsEqualTo(5432L);
    }

    [Test]
    public async Task Evaluate_AllLiteralTypes_StoresCorrectValues()
    {
        // Arrange
        var source = """
                     settings {
                       StringValue = "test"
                       IntValue = 42
                       FloatValue = 3.14
                       TrueValue = true
                       FalseValue = false
                       NullValue = null
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        await Assert.That(model.BaseSettings["StringValue"]!.GetValue<string>()).IsEqualTo("test");
        await Assert.That(model.BaseSettings["IntValue"]!.GetValue<long>()).IsEqualTo(42L);
        await Assert.That(model.BaseSettings["FloatValue"]!.GetValue<double>()).IsEqualTo(3.14);
        await Assert.That(model.BaseSettings["TrueValue"]!.GetValue<bool>()).IsEqualTo(true);
        await Assert.That(model.BaseSettings["FalseValue"]!.GetValue<bool>()).IsEqualTo(false);
        await Assert.That(model.BaseSettings["NullValue"]).IsNull();
    }

    [Test]
    public async Task Evaluate_EnvBlock_CreatesOverlay()
    {
        // Arrange
        var source = """
                     settings {
                       Port = 8080
                     }

                     env "Development" {
                       settings {
                         Port = 5000
                       }
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        await Assert.That(model.BaseSettings["Port"]!.GetValue<long>()).IsEqualTo(8080L);
        await Assert.That(model.EnvironmentOverlays).HasCount().EqualTo(1);
        await Assert.That(model.EnvironmentOverlays.ContainsKey("Development")).IsTrue();
        await Assert.That(model.EnvironmentOverlays["Development"]["Port"]!.GetValue<long>()).IsEqualTo(5000L);
    }

    [Test]
    public async Task Evaluate_MultipleEnvBlocks_CreatesMultipleOverlays()
    {
        // Arrange
        var source = """
                     settings {
                       Port = 8080
                     }

                     env "Development" {
                       settings {
                         Port = 5000
                       }
                     }

                     env "Production" {
                       settings {
                         Port = 80
                       }
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        await Assert.That(model.EnvironmentOverlays).HasCount().EqualTo(2);
        await Assert.That(model.EnvironmentOverlays["Development"]["Port"]!.GetValue<long>()).IsEqualTo(5000L);
        await Assert.That(model.EnvironmentOverlays["Production"]["Port"]!.GetValue<long>()).IsEqualTo(80L);
    }

    [Test]
    public async Task Evaluate_EnvWithDotPath_CreatesNestedOverlay()
    {
        // Arrange
        var source = """
                     settings {
                       Server.Port = 8080
                     }

                     env "Development" {
                       settings {
                         Server.Port = 5000
                         Logging.LogLevel.Default = "Debug"
                       }
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        var devOverlay = model.EnvironmentOverlays["Development"];
        var server = devOverlay["Server"] as JsonObject;
        await Assert.That(server!["Port"]!.GetValue<long>()).IsEqualTo(5000L);

        var logging = devOverlay["Logging"] as JsonObject;
        var logLevel = logging!["LogLevel"] as JsonObject;
        await Assert.That(logLevel!["Default"]!.GetValue<string>()).IsEqualTo("Debug");
    }

    [Test]
    public async Task Evaluate_ComplexExample_ConvertsCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       ApplicationName = "MyApp"
                       
                       Server {
                         Host = "0.0.0.0"
                         Port = 8080
                         AllowedHosts = ["localhost", "*.example.com"]
                       }
                       
                       Logging {
                         LogLevel {
                           Default = "Information"
                           Microsoft = "Warning"
                         }
                       }
                     }

                     env "Development" {
                       settings {
                         Server.Port = 5000
                         Logging.LogLevel.Default = "Debug"
                       }
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        // Base settings
        await Assert.That(model.BaseSettings["ApplicationName"]!.GetValue<string>()).IsEqualTo("MyApp");

        var server = model.BaseSettings["Server"] as JsonObject;
        await Assert.That(server!["Port"]!.GetValue<long>()).IsEqualTo(8080L);

        var allowedHosts = server["AllowedHosts"] as JsonArray;
        await Assert.That(allowedHosts).HasCount().EqualTo(2);

        // Environment overlay
        var devOverlay = model.EnvironmentOverlays["Development"];
        var devServer = devOverlay["Server"] as JsonObject;
        await Assert.That(devServer!["Port"]!.GetValue<long>()).IsEqualTo(5000L);
    }

    [Test]
    public async Task Evaluate_NoSettingsBlock_ThrowsException()
    {
        // Arrange
        var source = """
                     env "Development" {
                       settings {
                         Port = 5000
                       }
                     }
                     """;

        // Act & Assert
        await Assert.ThrowsAsync<EvaluatorException>(() => Task.FromResult(this.ParseAndEvaluate(source)));
    }

    [Test]
    public async Task Evaluate_MultipleSettingsBlocks_ThrowsException()
    {
        // Arrange
        var source = """
                     settings {
                       Port = 8080
                     }

                     settings {
                       Port = 9090
                     }
                     """;

        // Act & Assert
        await Assert.ThrowsAsync<EvaluatorException>(() => Task.FromResult(this.ParseAndEvaluate(source)));
    }

    [Test]
    public async Task Evaluate_DuplicateEnvNames_ThrowsException()
    {
        // Arrange
        var source = """
                     settings {
                       Port = 8080
                     }

                     env "Development" {
                       settings {
                         Port = 5000
                       }
                     }

                     env "Development" {
                       settings {
                         Port = 5001
                       }
                     }
                     """;

        // Act & Assert
        await Assert.ThrowsAsync<EvaluatorException>(() => Task.FromResult(this.ParseAndEvaluate(source)));
    }

    [Test]
    public async Task Evaluate_DotPathThroughNonObject_ThrowsException()
    {
        // Arrange
        var source = """
                     settings {
                       Items = [1, 2, 3]
                       Items.SubProperty = "value"
                     }
                     """;

        // Act & Assert
        await Assert.ThrowsAsync<EvaluatorException>(() => Task.FromResult(this.ParseAndEvaluate(source)));
    }

    [Test]
    public async Task Evaluate_MixedNestedBlockAndDotPath_MergesCorrectly()
    {
        // Arrange
        var source = """
                     settings {
                       Server {
                         Host = "localhost"
                       }
                       Server.Port = 8080
                     }
                     """;

        // Act
        var model = this.ParseAndEvaluate(source);

        // Assert
        var server = model.BaseSettings["Server"] as JsonObject;
        await Assert.That(server!["Host"]!.GetValue<string>()).IsEqualTo("localhost");
        await Assert.That(server["Port"]!.GetValue<long>()).IsEqualTo(8080L);
    }

    private SettingsModel ParseAndEvaluate(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.ParseFile();
        var evaluator = new Evaluator();
        return evaluator.Evaluate(ast);
    }
}
