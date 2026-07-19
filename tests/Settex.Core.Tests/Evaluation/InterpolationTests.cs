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
        var lexer = new Lexer(source, "test.settex");
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

        // V2 now supports member access on objects!
        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Message"]!.GetValue<string>()).IsEqualTo("Object value: 42");
    }

    [Test]
    public async Task Evaluate_EscapedInterpolation_StaysLiteral()
    {
        // "$${" lets a string legitimately contain "${...}" (shell templates,
        // regexes, runtime placeholders) without Settex evaluating it.
        var source = """
            settings {
                Path = "$${HOME}/bin"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json!["Path"]!.GetValue<string>()).IsEqualTo("${HOME}/bin");
    }

    [Test]
    public async Task Evaluate_EscapedAndRealInterpolationInSameString_BothHandled()
    {
        var source = """
            let port = 8080

            settings {
                Mixed = "$${NOT_INTERP} and ${port}"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json!["Mixed"]!.GetValue<string>()).IsEqualTo("${NOT_INTERP} and 8080");
    }

    [Test]
    public async Task Evaluate_EscapedInterpolation_DoesNotRequireTheVariableToExist()
    {
        // The whole point: an undefined name inside an escaped ${...} must not fail.
        var source = """
            settings {
                Template = "$${UNDEFINED_EVERYWHERE}"
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json!["Template"]!.GetValue<string>()).IsEqualTo("${UNDEFINED_EVERYWHERE}");
    }

    [Test]
    public async Task Evaluate_InterpolationWithTrailingTokens_ThrowsAnchoredError()
    {
        // "${a b}" must be rejected: previously the parser kept only 'a' and
        // silently dropped 'b'. The error must also point at the interpolated
        // string in the real file, not a phantom line 1 / column 1.
        var source = """
            let a = 1
            let b = 2

            settings {
                X = "${a b}"
            }
            """;

        var ex = await Assert.ThrowsAsync<ParserException>(() => Task.FromResult(CompileSource(source)));
        await Assert.That(ex!.Message).Contains("unexpected 'b'");
        await Assert.That(ex.Location.FilePath).IsEqualTo("test.settex");
        await Assert.That(ex.Location.Line).IsEqualTo(5);
    }

    [Test]
    public async Task Evaluate_ErrorInsideInterpolation_IsAnchoredToStringLocation()
    {
        // An error raised while parsing the embedded expression must be reported
        // at the interpolated string's location, not at the sub-lexer's origin.
        var source = """
            let port = 8080

            settings {
                Url = "x${port +}y"
            }
            """;

        var ex = await Assert.ThrowsAsync<ParserException>(() => Task.FromResult(CompileSource(source)));
        await Assert.That(ex!.Message).Contains("Invalid interpolation expression");
        await Assert.That(ex.Location.FilePath).IsEqualTo("test.settex");
        await Assert.That(ex.Location.Line).IsEqualTo(4);
    }
}
