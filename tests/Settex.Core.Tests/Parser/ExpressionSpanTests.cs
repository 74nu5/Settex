namespace Settex.Core.Tests.Parser;

using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;

using TUnit.Core;

/// <summary>
/// An expression node's location is not its extent. A binary or unary operator node
/// carries the operator's position, a member access carries its first segment's, and a
/// parenthesised expression drops the parentheses entirely — so an assignment spanned
/// to its value node stopped short of the value, and hovering the tail of an expression
/// was judged to be outside the assignment it belongs to. The span comes from the last
/// consumed token now, which is exact for every shape.
/// </summary>
public class ExpressionSpanTests
{
    [Test]
    public async Task Assignment_WithBinaryOperatorValue_SpansPastTheRightOperandAsync()
    {
        // Line 2 is "    B = 1 + 2": the trailing 2 sits at column 13.
        var ast = Parse("settings {\n    B = 1 + 2\n}");
        var assignment = FirstAssignment(ast);

        await Assert.That(assignment.Location.EndLine).IsEqualTo(2);
        await Assert.That(assignment.Location.EffectiveEndColumn).IsGreaterThanOrEqualTo(14);
    }

    [Test]
    public async Task Assignment_WithMemberAccessValue_SpansPastTheLastSegmentAsync()
    {
        // Line 3 is "    A = u.Name": the value ends at column 14.
        var ast = Parse("let u = 1\nsettings {\n    A = u.Name\n}");
        var assignment = FirstAssignment(ast);

        await Assert.That(assignment.Location.EndLine).IsEqualTo(3);
        await Assert.That(assignment.Location.EffectiveEndColumn).IsGreaterThanOrEqualTo(15);
    }

    [Test]
    public async Task Assignment_WithParenthesisedValue_SpansPastTheClosingParenAsync()
    {
        // Line 2 is "    C = (1 + 2)": the closing paren sits at column 15.
        var ast = Parse("settings {\n    C = (1 + 2)\n}");
        var assignment = FirstAssignment(ast);

        await Assert.That(assignment.Location.EffectiveEndColumn).IsGreaterThanOrEqualTo(16);
    }

    [Test]
    public async Task Assignment_WithUnaryOperatorValue_SpansPastTheOperandAsync()
    {
        // Line 2 is "    E = -42": the operand ends at column 11.
        var ast = Parse("settings {\n    E = -42\n}");
        var assignment = FirstAssignment(ast);

        await Assert.That(assignment.Location.EffectiveEndColumn).IsGreaterThanOrEqualTo(12);
    }

    [Test]
    public async Task Assignment_WithCondition_StillSpansToTheConditionAsync()
    {
        // The condition comes last, so it, not the value, must bound the span.
        var ast = Parse("settings {\n    D = 1 if true\n}");
        var assignment = FirstAssignment(ast);

        await Assert.That(assignment.Location.EffectiveEndColumn).IsGreaterThanOrEqualTo(18);
    }

    /// <summary>
    /// Env-level lets are prepended into the settings block's statement list. The block
    /// kept its own location, so those lets became children that begin before their
    /// parent — an inverted containment that scope resolution and document symbols both
    /// assume cannot happen, and an invalid range once mapped to LSP.
    /// </summary>
    [Test]
    public async Task EnvLevelLet_IsContainedByItsEnclosingBlockAsync()
    {
        // 1: env "Dev" {
        // 2:     let z = 2
        // 3:     settings {
        // 4:         A = z
        // 5:     }
        // 6: }
        var ast = Parse("env \"Dev\" {\n    let z = 2\n    settings {\n        A = z\n    }\n}");
        var env = ast.Statements.OfType<EnvBlockNode>().Single();
        var block = env.SettingsBlock.Block;
        var letNode = block.Statements.OfType<LetNode>().Single();

        await Assert.That(letNode.Location.Line).IsEqualTo(2);

        // A parent must start no later than its first child and end no earlier.
        await Assert.That(block.Location.Line).IsLessThanOrEqualTo(letNode.Location.Line);
        await Assert.That(env.SettingsBlock.Location.Line).IsLessThanOrEqualTo(letNode.Location.Line);
        await Assert.That(block.Location.EffectiveEndLine).IsGreaterThanOrEqualTo(letNode.Location.EffectiveEndLine);
    }

    [Test]
    public async Task EnvWithoutLevelLets_KeepsTheSettingsBlockLocationAsync()
    {
        // The widening must apply only where lets were actually hoisted in.
        var ast = Parse("env \"Dev\" {\n    settings {\n        A = 1\n    }\n}");
        var env = ast.Statements.OfType<EnvBlockNode>().Single();

        await Assert.That(env.SettingsBlock.Location.Line).IsEqualTo(2);
    }

    private static AssignmentNode FirstAssignment(FileNode ast)
        => ast.Statements
            .OfType<SettingsBlockNode>()
            .Single()
            .Block.Statements
            .OfType<AssignmentNode>()
            .First();

    private static FileNode Parse(string source)
    {
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());
        return parser.Parse();
    }
}
