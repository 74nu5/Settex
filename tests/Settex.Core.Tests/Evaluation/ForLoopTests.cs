namespace Settex.Core.Tests.Evaluation;

using Settex.Core.Evaluation;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using TUnit.Assertions;
using TUnit.Core;

/// <summary>
///     Tests for for loops in arrays.
/// </summary>
public sealed class ForLoopTests
{
    /// <summary>
    ///     Test simple for loop generating array items.
    /// </summary>
    [Test]
    public async Task ForLoop_SimpleIteration_GeneratesItems()
    {
        var source = """
            let numbers = [1, 2, 3]

            settings {
                Items = [ for n in numbers { item { Value = n } } ]
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var items = model.BaseSettings["Items"]?.AsArray();
        await Assert.That(items).IsNotNull();
        await Assert.That(items!.Count).IsEqualTo(3);

        var item0 = items[0]?.AsObject();
        await Assert.That(item0!["Value"]?.GetValue<long>()).IsEqualTo(1);

        var item1 = items[1]?.AsObject();
        await Assert.That(item1!["Value"]?.GetValue<long>()).IsEqualTo(2);

        var item2 = items[2]?.AsObject();
        await Assert.That(item2!["Value"]?.GetValue<long>()).IsEqualTo(3);
    }

    /// <summary>
    ///     Test for loop with string interpolation.
    /// </summary>
    [Test]
    public async Task ForLoop_WithInterpolation_GeneratesItems()
    {
        var source = """
            let services = ["auth", "api"]

            settings {
                Services = [ for s in services { item { Name = s, Url = "http://localhost/${s}" } } ]
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var services = model.BaseSettings["Services"]?.AsArray();
        await Assert.That(services).IsNotNull();
        await Assert.That(services!.Count).IsEqualTo(2);

        var service0 = services[0]?.AsObject();
        await Assert.That(service0!["Name"]?.GetValue<string>()).IsEqualTo("auth");
        await Assert.That(service0["Url"]?.GetValue<string>()).IsEqualTo("http://localhost/auth");

        var service1 = services[1]?.AsObject();
        await Assert.That(service1!["Name"]?.GetValue<string>()).IsEqualTo("api");
        await Assert.That(service1["Url"]?.GetValue<string>()).IsEqualTo("http://localhost/api");
    }

    /// <summary>
    ///     Test for loop with empty collection.
    /// </summary>
    [Test]
    public async Task ForLoop_EmptyCollection_GeneratesEmptyArray()
    {
        var source = """
            let items = []

            settings {
                Results = [ for i in items { item { Value = i } } ]
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var results = model.BaseSettings["Results"]?.AsArray();
        await Assert.That(results).IsNotNull();
        await Assert.That(results!.Count).IsEqualTo(0);
    }

    /// <summary>
    ///     Test for loop over non-array throws error.
    /// </summary>
    [Test]
    public async Task ForLoop_NonArrayCollection_ThrowsError()
    {
        var source = """
            let value = "not an array"

            settings {
                Items = [ for i in value { item { Value = i } } ]
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();

        await Assert.ThrowsAsync<EvaluatorException>(() => Task.FromResult(evaluator.Evaluate(ast)));
    }

    /// <summary>
    ///     Test for loop with mixed regular items and for loops.
    /// </summary>
    [Test]
    public async Task ForLoop_MixedWithRegularItems_GeneratesAllItems()
    {
        var source = """
            let numbers = [10, 20]

            settings {
                Values = [ "first", for n in numbers { item { Number = n } }, "last" ]
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var values = model.BaseSettings["Values"]?.AsArray();
        await Assert.That(values).IsNotNull();
        await Assert.That(values!.Count).IsEqualTo(4);

        await Assert.That(values[0]?.GetValue<string>()).IsEqualTo("first");
        
        var obj1 = values[1]?.AsObject();
        await Assert.That(obj1!["Number"]?.GetValue<long>()).IsEqualTo(10);
        
        var obj2 = values[2]?.AsObject();
        await Assert.That(obj2!["Number"]?.GetValue<long>()).IsEqualTo(20);
        
        await Assert.That(values[3]?.GetValue<string>()).IsEqualTo("last");
    }

    /// <summary>
    ///     Test for loop iterator variable scope.
    /// </summary>
    [Test]
    public async Task ForLoop_IteratorVariable_IsLocalToForScope()
    {
        var source = """
            let items = [1, 2]

            settings {
                Values = [ for item in items { item { Value = item } } ]
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var values = model.BaseSettings["Values"]?.AsArray();
        await Assert.That(values).IsNotNull();
        await Assert.That(values!.Count).IsEqualTo(2);
    }

    /// <summary>
    ///     Test for loop with expressions in iterator collection.
    /// </summary>
    [Test]
    public async Task ForLoop_WithExpressionCollection_GeneratesItems()
    {
        var source = """
            let a = [1, 2]
            let b = [3, 4]

            settings {
                Values = [ for n in a { item { Value = n } } ]
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var values = model.BaseSettings["Values"]?.AsArray();
        await Assert.That(values).IsNotNull();
        await Assert.That(values!.Count).IsEqualTo(2);
    }

    /// <summary>
    ///     Test for loop accessing outer scope variables.
    /// </summary>
    [Test]
    public async Task ForLoop_AccessesOuterScopeVariables()
    {
        var source = """
            let prefix = "Item"
            let numbers = [1, 2]

            settings {
                Items = [ for n in numbers { item { Name = "${prefix}_${n}" } } ]
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        var items = model.BaseSettings["Items"]?.AsArray();
        await Assert.That(items).IsNotNull();
        await Assert.That(items!.Count).IsEqualTo(2);

        var item0 = items[0]?.AsObject();
        await Assert.That(item0!["Name"]?.GetValue<string>()).IsEqualTo("Item_1");

        var item1 = items[1]?.AsObject();
        await Assert.That(item1!["Name"]?.GetValue<string>()).IsEqualTo("Item_2");
    }

    /// <summary>
    ///     Test for loop in env overlay.
    /// </summary>
    [Test]
    public async Task ForLoop_InEnvOverlay_GeneratesItems()
    {
        var source = """
            let devServers = ["server1", "server2"]

            settings {
                Servers = []
            }

            env "Development" {
                settings {
                    Servers = [ for s in devServers { item { Name = s } } ]
                }
            }
            """;

        var lexer = new Lexer(source, "test.settex");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        var evaluator = new Evaluator();
        var model = evaluator.Evaluate(ast);

        // Base should have empty array
        var baseServers = model.BaseSettings["Servers"]?.AsArray();
        await Assert.That(baseServers).IsNotNull();
        await Assert.That(baseServers!.Count).IsEqualTo(0);

        // Development should have 2 servers
        var devServers = model.EnvironmentOverlays["Development"]["Servers"]?.AsArray();
        await Assert.That(devServers).IsNotNull();
        await Assert.That(devServers!.Count).IsEqualTo(2);

        var server0 = devServers[0]?.AsObject();
        await Assert.That(server0!["Name"]?.GetValue<string>()).IsEqualTo("server1");

        var server1 = devServers[1]?.AsObject();
        await Assert.That(server1!["Name"]?.GetValue<string>()).IsEqualTo("server2");
    }
}
