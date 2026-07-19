namespace Settex.Core.Tests.Evaluation;

using System.Text.Json.Nodes;

using Settex.Core.Evaluation;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;
using Settex.Core.Resolution;

using TUnit.Core;

public class ConditionalAssignmentTests
{
    private static SettingsModel CompileSource(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, "test.settex");
        var ast = parser.Parse();

        var includeResolver = new IncludeResolver();
        var resolvedStatements = includeResolver.ResolveIncludes(ast, "test.settex");

        var resolvedAst = new FileNode(resolvedStatements, ast.Location);

        var evaluator = new Evaluator();
        return evaluator.Evaluate(resolvedAst);
    }

    [Test]
    public async Task Evaluate_ConditionalAssignmentTrue_AppliesAssignment()
    {
        var source = """
            settings {
                LogLevel = "Debug" if true
            }
            """;

        var model = CompileSource(source);
        var json = model.BaseSettings;

        await Assert.That(json).IsNotNull();
        await Assert.That(json!["LogLevel"]!.GetValue<string>()).IsEqualTo("Debug");
    }

    [Test]
    public async Task Evaluate_ConditionalAssignmentFalse_SkipsAssignment()
    {
        var source = """
            settings {
                LogLevel = "Debug" if false
            }
            """;

        var model = CompileSource(source);
        var json = model.BaseSettings;

        await Assert.That(json).IsNotNull();
        await Assert.That(json.ContainsKey("LogLevel")).IsEqualTo(false);
    }

    [Test]
    public async Task Evaluate_ConditionalAssignmentWithEnvVariableInBase_UsesBaseValue()
    {
        var source = """
            settings {
                LogLevel = "Debug" if env == "Base"
            }
            """;

        var model = CompileSource(source);
        var json = model.BaseSettings;

        await Assert.That(json).IsNotNull();
        await Assert.That(json!["LogLevel"]!.GetValue<string>()).IsEqualTo("Debug");
    }

    [Test]
    public async Task Evaluate_ConditionalAssignmentWithEnvVariableInEnv_UsesEnvName()
    {
        var source = """
            settings {
                LogLevel = "Info"
            }

            env "Development" {
                settings {
                    LogLevel = "Debug" if env == "Development"
                }
            }

            env "Production" {
                settings {
                    LogLevel = "Debug" if env == "Development"
                }
            }
            """;

        var model = CompileSource(source);

        // Development should have Debug
        var devJson = model.EnvironmentOverlays["Development"];
        await Assert.That(devJson).IsNotNull();
        await Assert.That(devJson!["LogLevel"]!.GetValue<string>()).IsEqualTo("Debug");

        // Production should NOT have LogLevel (condition is false)
        var prodJson = model.EnvironmentOverlays["Production"];
        await Assert.That(prodJson).IsNotNull();
        await Assert.That(prodJson.ContainsKey("LogLevel")).IsEqualTo(false);
    }

    [Test]
    public async Task Evaluate_ConditionalAssignmentWithExpression_EvaluatesCondition()
    {
        var source = """
            let port = 8080

            settings {
                UseHttps = true if port == 443
                UseHttps = false if port == 8080
            }
            """;

        var model = CompileSource(source);
        var json = model.BaseSettings;

        await Assert.That(json).IsNotNull();
        await Assert.That(json!["UseHttps"]!.GetValue<bool>()).IsEqualTo(false);
    }

    [Test]
    public async Task Evaluate_MultipleConditionalAssignments_OnlyAppliesMatching()
    {
        var source = """
            settings {
                LogLevel = "Error" if env == "Production"
                LogLevel = "Warning" if env == "Staging"
                LogLevel = "Debug" if env == "Base"
            }
            """;

        var model = CompileSource(source);
        var json = model.BaseSettings;

        await Assert.That(json).IsNotNull();
        await Assert.That(json!["LogLevel"]!.GetValue<string>()).IsEqualTo("Debug");
    }

    [Test]
    public async Task Evaluate_ConditionalAssignmentNonBoolCondition_ThrowsException()
    {
        var source = """
            settings {
                LogLevel = "Debug" if "not a bool"
            }
            """;

        await Assert.ThrowsAsync<EvaluatorException>(() => Task.FromResult(CompileSource(source)));
    }

    [Test]
    public async Task Evaluate_ConditionalAssignmentWithComplexCondition_EvaluatesCorrectly()
    {
        var source = """
            let isDev = true
            let isDebug = true

            settings {
                LogLevel = "Debug" if isDev and isDebug
            }
            """;

        var model = CompileSource(source);
        var json = model.BaseSettings;

        await Assert.That(json).IsNotNull();
        await Assert.That(json!["LogLevel"]!.GetValue<string>()).IsEqualTo("Debug");
    }

    [Test]
    public async Task Evaluate_ConditionalAssignmentInNestedObject_WorksCorrectly()
    {
        var source = """
            settings {
                Logging {
                    LogLevel = "Debug" if env == "Base"
                }
            }
            """;

        var model = CompileSource(source);
        var json = model.BaseSettings;

        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Logging"]).IsNotNull();
        await Assert.That(json["Logging"]!["LogLevel"]!.GetValue<string>()).IsEqualTo("Debug");
    }

    [Test]
    public async Task Evaluate_ConditionalWithNullCoalesce_WorksCorrectly()
    {
        var source = """
            let value = null
            let defaultValue = "default"

            settings {
                Setting = value ?? defaultValue if true
            }
            """;

        var model = CompileSource(source);
        var json = model.BaseSettings;

        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Setting"]!.GetValue<string>()).IsEqualTo("default");
    }
}
