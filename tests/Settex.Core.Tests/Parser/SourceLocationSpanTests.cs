namespace Settex.Core.Tests.Parser;

using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;

using TUnit.Core;

/// <summary>
/// Block-bearing nodes must carry a real end span (the closing brace), so editors
/// can build multi-line symbol ranges and resolve scopes through the whole body.
/// </summary>
public class SourceLocationSpanTests
{
    [Test]
    public async Task SettingsBlock_SpansToClosingBraceAsync()
    {
        // 1: settings {
        // 2:     A = 1
        // 3: }
        var ast = Parse("settings {\n    A = 1\n}");
        var settings = ast.Statements.OfType<SettingsBlockNode>().Single();

        await Assert.That(settings.Location.Line).IsEqualTo(1);
        await Assert.That(settings.Location.EndLine).IsEqualTo(3);
    }

    [Test]
    public async Task EnvBlock_SpansToClosingBraceAsync()
    {
        // 1: settings { A = 1 }
        // 2:
        // 3: env "Prod" {
        // 4:     settings {
        // 5:         B = 2
        // 6:     }
        // 7: }
        var ast = Parse("settings { A = 1 }\n\nenv \"Prod\" {\n    settings {\n        B = 2\n    }\n}");
        var env = ast.Statements.OfType<EnvBlockNode>().Single();

        await Assert.That(env.Location.Line).IsEqualTo(3);
        await Assert.That(env.Location.EndLine).IsEqualTo(7);
    }

    [Test]
    public async Task NestedBlock_SpansToClosingBraceAsync()
    {
        // 1: settings {
        // 2:     Server {
        // 3:         Port = 1
        // 4:     }
        // 5: }
        var ast = Parse("settings {\n    Server {\n        Port = 1\n    }\n}");
        var settings = ast.Statements.OfType<SettingsBlockNode>().Single();
        var nested = settings.Block.Statements.OfType<NestedBlockNode>().Single();

        await Assert.That(nested.Location.Line).IsEqualTo(2);
        await Assert.That(nested.Location.EndLine).IsEqualTo(4);
    }

    [Test]
    public async Task TokenBasedNode_FallsBackToItsOwnLineAsync()
    {
        // A `let` is located on its keyword token: no end span, so the effective end
        // derives from Line and Column + Length.
        var ast = Parse("let a = 1\n\nsettings { A = a }");
        var let = ast.Statements.OfType<LetNode>().Single();

        await Assert.That(let.Location.EndLine).IsNull();
        await Assert.That(let.Location.EffectiveEndLine).IsEqualTo(let.Location.Line);
        await Assert.That(let.Location.EffectiveEndColumn)
            .IsEqualTo(let.Location.Column + let.Location.Length);
    }

    [Test]
    public async Task Assignment_SpansFromPathToEndOfValueAsync()
    {
        // Assignments carry a span so an editor can tell whether a position is really
        // on the assignment rather than merely on the same line.
        var ast = Parse("settings {\n    Server.Port = 8080\n}");
        var settings = ast.Statements.OfType<SettingsBlockNode>().Single();
        var assignment = settings.Block.Statements.OfType<AssignmentNode>().Single();

        await Assert.That(assignment.Location.Line).IsEqualTo(2);
        await Assert.That(assignment.Location.EndLine).IsEqualTo(2);

        // The span must reach past the path, over the value.
        await Assert.That(assignment.Location.EffectiveEndColumn)
            .IsGreaterThan(assignment.Location.Column + "Server.Port".Length);
    }

    [Test]
    public async Task Assignment_WithMultiLineArray_SpansToClosingBracketAsync()
    {
        // 1: settings {   2: Ports = [   3: 1   4: 2   5: ]   6: }
        var ast = Parse("settings {\n    Ports = [\n        1\n        2\n    ]\n}");
        var settings = ast.Statements.OfType<SettingsBlockNode>().Single();
        var assignment = settings.Block.Statements.OfType<AssignmentNode>().Single();

        await Assert.That(assignment.Location.Line).IsEqualTo(2);
        await Assert.That(assignment.Location.EndLine).IsEqualTo(5);
    }

    [Test]
    public async Task File_SpansWholeDocumentAsync()
    {
        var ast = Parse("settings {\n    A = 1\n}");

        await Assert.That(ast.Location.EndLine).IsNotNull();
        await Assert.That(ast.Location.EndLine!.Value).IsGreaterThanOrEqualTo(3);
    }

    private static FileNode Parse(string source)
    {
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());
        return parser.Parse();
    }
}
