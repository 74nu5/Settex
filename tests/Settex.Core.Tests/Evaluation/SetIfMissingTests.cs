namespace Settex.Core.Tests.Evaluation;

using Settex.Core.Evaluation;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using TUnit.Assertions;
using TUnit.Core;

/// <summary>
///     Tests for the set-if-missing operator (:=).
/// </summary>
public sealed class SetIfMissingTests
{
    /// <summary>
    ///     Test that := does not override an existing value in base settings.
    /// </summary>
    [Test]
    public async Task SetIfMissing_ExistingValueInBase_DoesNotOverride()
    {
        var source = """
            settings {
                Port = 5000
                Port := 8080
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var port = model.BaseSettings["Port"];
        await Assert.That(port?.GetValue<long>()).IsEqualTo(5000);
    }

    /// <summary>
    ///     Test that := sets the value when the key is missing in base settings.
    /// </summary>
    [Test]
    public async Task SetIfMissing_MissingKeyInBase_SetsValue()
    {
        var source = """
            settings {
                Port := 8080
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var port = model.BaseSettings["Port"];
        await Assert.That(port?.GetValue<long>()).IsEqualTo(8080);
    }

    /// <summary>
    ///     Test that := in env does not override when key exists in base settings.
    /// </summary>
    [Test]
    public async Task SetIfMissing_ExistingInBase_EnvDoesNotOverride()
    {
        var source = """
            settings {
                Port = 8080
            }

            env "Production" {
                settings {
                    Port := 9000
                }
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        // Base should have 8080
        await Assert.That(model.BaseSettings["Port"]?.GetValue<long>()).IsEqualTo(8080);

        // Production overlay should be empty (key not set because it exists in base)
        await Assert.That(model.EnvironmentOverlays["Production"].ContainsKey("Port")).IsFalse();
    }

    /// <summary>
    ///     Test that := in env does not override when key exists in the same env overlay.
    /// </summary>
    [Test]
    public async Task SetIfMissing_ExistingInEnv_DoesNotOverride()
    {
        var source = """
            settings {
                Host = "localhost"
            }

            env "Development" {
                settings {
                    Port = 3000
                    Port := 5000
                }
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        // Development should have Port = 3000
        await Assert.That(model.EnvironmentOverlays["Development"]["Port"]?.GetValue<long>()).IsEqualTo(3000);
    }

    /// <summary>
    ///     Test that := in env sets value when key is missing in both base and overlay.
    /// </summary>
    [Test]
    public async Task SetIfMissing_MissingInBothBaseAndEnv_SetsValue()
    {
        var source = """
            settings {
                Host = "localhost"
            }

            env "Development" {
                settings {
                    Port := 3000
                }
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        // Development should have Port = 3000
        await Assert.That(model.EnvironmentOverlays["Development"]["Port"]?.GetValue<long>()).IsEqualTo(3000);
    }

    /// <summary>
    ///     Test that null is considered as an existing value (present).
    /// </summary>
    [Test]
    public async Task SetIfMissing_NullValueExists_DoesNotOverride()
    {
        var source = """
            settings {
                ApiKey = null
                ApiKey := "default-key"
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var apiKey = model.BaseSettings["ApiKey"];
        await Assert.That(apiKey).IsNull();
    }

    /// <summary>
    ///     Test := with nested paths in base settings.
    /// </summary>
    [Test]
    public async Task SetIfMissing_NestedPath_ExistingValue_DoesNotOverride()
    {
        var source = """
            settings {
                Server {
                    Port = 8080
                    Port := 9000
                }
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var server = model.BaseSettings["Server"]?.AsObject();
        await Assert.That(server).IsNotNull();
        await Assert.That(server!["Port"]?.GetValue<long>()).IsEqualTo(8080);
    }

    /// <summary>
    ///     Test := with nested paths when key is missing.
    /// </summary>
    [Test]
    public async Task SetIfMissing_NestedPath_MissingKey_SetsValue()
    {
        var source = """
            settings {
                Server {
                    Host := "localhost"
                }
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var server = model.BaseSettings["Server"]?.AsObject();
        await Assert.That(server).IsNotNull();
        await Assert.That(server!["Host"]?.GetValue<string>()).IsEqualTo("localhost");
    }

    /// <summary>
    ///     Test := combined with if condition.
    /// </summary>
    [Test]
    public async Task SetIfMissing_WithCondition_BothMustBeTrue()
    {
        var source = """
            settings {
                LogLevel := "Info" if false
                DebugMode := true if true
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        // LogLevel should not be set (condition is false)
        await Assert.That(model.BaseSettings.ContainsKey("LogLevel")).IsFalse();

        // DebugMode should be set (condition is true and key is missing)
        await Assert.That(model.BaseSettings["DebugMode"]?.GetValue<bool>()).IsTrue();
    }

    /// <summary>
    ///     Test := in env with nested paths checking base settings.
    /// </summary>
    [Test]
    public async Task SetIfMissing_EnvNestedPath_ExistsInBase_DoesNotOverride()
    {
        var source = """
            settings {
                Database {
                    Host = "prod-db"
                }
            }

            env "Development" {
                settings {
                    Database.Host := "localhost"
                }
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        // Base should have Database.Host = "prod-db"
        var baseDb = model.BaseSettings["Database"]?.AsObject();
        await Assert.That(baseDb!["Host"]?.GetValue<string>()).IsEqualTo("prod-db");

        // Development overlay should NOT have Database or Database.Host (key exists in base)
        await Assert.That(model.EnvironmentOverlays["Development"].ContainsKey("Database")).IsFalse();
    }
}
