namespace Settex.Core.Tests.Evaluation;

using System.Text.Json.Nodes;

using Settex.Core.Evaluation;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;
using Settex.Core.Resolution;

using TUnit.Core;

public class ExpressionEvaluationTests
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
    public async Task Evaluate_Addition_ReturnsCorrectResult()
    {
        var source = """
            let result = 5 + 3

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<long>()).IsEqualTo(8L);
    }

    [Test]
    public async Task Evaluate_Subtraction_ReturnsCorrectResult()
    {
        var source = """
            let result = 10 - 3

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<long>()).IsEqualTo(7L);
    }

    [Test]
    public async Task Evaluate_Multiplication_ReturnsCorrectResult()
    {
        var source = """
            let result = 4 * 3

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<long>()).IsEqualTo(12L);
    }

    [Test]
    public async Task Evaluate_Division_ReturnsCorrectResult()
    {
        var source = """
            let result = 15 / 3

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<long>()).IsEqualTo(5L);
    }

    [Test]
    public async Task Evaluate_PrecedenceMultiplicationBeforeAddition_ReturnsCorrectResult()
    {
        var source = """
            let result = 2 + 3 * 4

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<long>()).IsEqualTo(14L);
    }

    [Test]
    public async Task Evaluate_PrecedenceAdditionThenMultiplication_ReturnsCorrectResult()
    {
        var source = """
            let result = 5 + 3 * 2

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<long>()).IsEqualTo(11L);
    }

    [Test]
    public async Task Evaluate_Negation_ReturnsCorrectResult()
    {
        var source = """
            let result = -5

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<long>()).IsEqualTo(-5L);
    }

    [Test]
    public async Task Evaluate_EqualityTrue_ReturnsTrue()
    {
        var source = """
            let result = 5 == 5

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(true);
    }

    [Test]
    public async Task Evaluate_EqualityFalse_ReturnsFalse()
    {
        var source = """
            let result = 5 == 3

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(false);
    }

    [Test]
    public async Task Evaluate_InequalityTrue_ReturnsTrue()
    {
        var source = """
            let result = 5 != 3

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(true);
    }

    [Test]
    public async Task Evaluate_LessThanTrue_ReturnsTrue()
    {
        var source = """
            let result = 3 < 5

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(true);
    }

    [Test]
    public async Task Evaluate_GreaterThanTrue_ReturnsTrue()
    {
        var source = """
            let result = 10 > 5

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(true);
    }

    [Test]
    public async Task Evaluate_LogicalAndTrue_ReturnsTrue()
    {
        var source = """
            let result = true and true

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(true);
    }

    [Test]
    public async Task Evaluate_LogicalAndFalse_ReturnsFalse()
    {
        var source = """
            let result = true and false

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(false);
    }

    [Test]
    public async Task Evaluate_LogicalOrTrue_ReturnsTrue()
    {
        var source = """
            let result = false or true

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(true);
    }

    [Test]
    public async Task Evaluate_LogicalOrFalse_ReturnsFalse()
    {
        var source = """
            let result = false or false

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(false);
    }

    [Test]
    public async Task Evaluate_LogicalNotTrue_ReturnsFalse()
    {
        var source = """
            let result = not true

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(false);
    }

    [Test]
    public async Task Evaluate_LogicalNotFalse_ReturnsTrue()
    {
        var source = """
            let result = not false

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(true);
    }

    [Test]
    public async Task Evaluate_NullCoalescingWithNull_ReturnsDefaultValue()
    {
        var source = """
            let result = null ?? "default"

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<string>()).IsEqualTo("default");
    }

    [Test]
    public async Task Evaluate_NullCoalescingWithNonNull_ReturnsFirstValue()
    {
        var source = """
            let result = "first" ?? "default"

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<string>()).IsEqualTo("first");
    }

    [Test]
    public async Task Evaluate_ComplexExpression_ReturnsCorrectResult()
    {
        var source = """
            let a = 5
            let b = 3
            let c = 2
            let result = a + b * c

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<long>()).IsEqualTo(11L);
    }

    [Test]
    public async Task Evaluate_StringEqualityTrue_ReturnsTrue()
    {
        var source = """
            let result = "hello" == "hello"

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(true);
    }

    [Test]
    public async Task Evaluate_StringEqualityFalse_ReturnsFalse()
    {
        var source = """
            let result = "hello" == "world"

            settings {
                Value = result
            }
            """;

        var json = CompileSource(source);
        await Assert.That(json).IsNotNull();
        await Assert.That(json!["Value"]!.GetValue<bool>()).IsEqualTo(false);
    }

    [Test]
    public async Task Evaluate_DivisionByZero_ThrowsException()
    {
        var source = """
            let result = 10 / 0

            settings {
                Value = result
            }
            """;

        await Assert.ThrowsAsync(() => Task.FromResult(CompileSource(source)));
    }

    [Test]
    public async Task Evaluate_StringPlusNumber_ThrowsException()
    {
        var source = """
            let result = "hello" + 5

            settings {
                Value = result
            }
            """;

        await Assert.ThrowsAsync(() => Task.FromResult(CompileSource(source)));
    }

    [Test]
    public async Task Evaluate_AndWithNonBool_ThrowsException()
    {
        var source = """
            let result = 5 and true

            settings {
                Value = result
            }
            """;

        await Assert.ThrowsAsync(() => Task.FromResult(CompileSource(source)));
    }
}
