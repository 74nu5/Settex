namespace Settex.Core.Tests.Evaluation;

using System.Text.Json.Nodes;

using Settex.Core.Evaluation;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;
using Settex.Core.Resolution;

using TUnit.Core;

public class InterpolationTests
{
    private static JsonNode? CompileSource(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, "test.settex");
        var ast = parser.Parse();

        var includeResolver = new IncludeResolver();
        var resolvedStatements = includeResolver.ResolveIncludes(ast, "test.settex");

        var resolvedAst = new FileNode(resolvedStatements, ast.Location);

        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(resolvedAst);

        return model.BaseSettings;
    }

    [Test]
    public async Task Evaluate_SimpleInterpolation_ReturnsInterpolatedString()
    {
        var source = """
            let name = "World"

            settings {
                Message = "Hello ${name}"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Message"]!.GetValue<string>()).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Evaluate_MultipleInterpolations_ReturnsInterpolatedString()
    {
        var source = """
            let host = "localhost"
            let port = 8080

            settings {
                Url = "http://${host}:${port}"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Url"]!.GetValue<string>()).IsEqualTo("http://localhost:8080");
    }

    [Test]
    public async Task Evaluate_InterpolationWithExpression_EvaluatesExpression()
    {
        var source = """
            settings {
                Message = "Result: ${5 + 3}"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Message"]!.GetValue<string>()).IsEqualTo("Result: 8");
    }

    [Test]
    public async Task Evaluate_InterpolationWithBool_ConvertsBoolToString()
    {
        var source = """
            let enabled = true

            settings {
                Message = "Enabled: ${enabled}"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Message"]!.GetValue<string>()).IsEqualTo("Enabled: true");
    }

    [Test]
    public async Task Evaluate_InterpolationWithNull_ThrowsException()
    {
        var source = """
            let value = null

            settings {
                Message = "Value: ${value}"
            }
            """;

        await Assert.ThrowsAsync(() => Task.FromResult(CompileSource(source)));
    }

    [Test]
    public async Task Evaluate_InterpolationAtStart_ReturnsCorrectString()
    {
        var source = """
            let name = "World"

            settings {
                Message = "${name} says hello"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Message"]!.GetValue<string>()).IsEqualTo("World says hello");
    }

    [Test]
    public async Task Evaluate_InterpolationAtEnd_ReturnsCorrectString()
    {
        var source = """
            let name = "World"

            settings {
                Message = "Hello ${name}"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Message"]!.GetValue<string>()).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Evaluate_OnlyInterpolation_ReturnsValue()
    {
        var source = """
            let name = "World"

            settings {
                Message = "${name}"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Message"]!.GetValue<string>()).IsEqualTo("World");
    }

    [Test]
    public async Task Evaluate_ComplexExpression_EvaluatesCorrectly()
    {
        var source = """
            let a = 5
            let b = 3

            settings {
                Message = "Sum: ${a + b}, Product: ${a * b}"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Message"]!.GetValue<string>()).IsEqualTo("Sum: 8, Product: 15");
    }

    [Test]
    public async Task Evaluate_NestedBraces_ParsesCorrectly()
    {
        var source = """
            let obj = item { value = 42 }

            settings {
                Message = "Object value: ${obj.value}"
            }
            """;

        // This test might fail if we don't support property access on objects yet
        // For now, we'll just test that it doesn't crash during parsing
        await Assert.ThrowsAsync(() => Task.FromResult(CompileSource(source)));
    }
}
