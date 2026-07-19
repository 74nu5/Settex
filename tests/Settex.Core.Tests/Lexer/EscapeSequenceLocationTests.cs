namespace Settex.Core.Tests.Lexer;

using Settex.Core.Lexer;

using TUnit.Core;

/// <summary>
/// An invalid escape sequence is detected after the cursor has already moved past the
/// backslash, so the reported span used to start on the escape character and cover it
/// plus whatever followed — highlighting <c>$H</c> instead of the <c>\$</c> at fault.
/// </summary>
public class EscapeSequenceLocationTests
{
    [Test]
    public async Task InvalidDollarEscape_PointsAtTheBackslashAsync()
    {
        // Column 1234567890
        //        A = "\$HOME"   -> the backslash sits at column 10.
        const string source = "settings {\n    A = \"\\$HOME\"\n}";

        var exception = Assert.Throws<LexerException>(() => new Lexer(source).Tokenize());

        await Assert.That(exception!.Location).IsNotNull();
        await Assert.That(exception.Location!.Line).IsEqualTo(2);
        await Assert.That(exception.Location.Column).IsEqualTo(10);
        await Assert.That(exception.Location.Length).IsEqualTo(2);

        // The message points at the supported syntax rather than just refusing.
        await Assert.That(exception.Message).Contains("$${");
    }

    [Test]
    public async Task InvalidGenericEscape_PointsAtTheBackslashAsync()
    {
        const string source = "settings {\n    A = \"\\qoops\"\n}";

        var exception = Assert.Throws<LexerException>(() => new Lexer(source).Tokenize());

        await Assert.That(exception!.Location!.Column).IsEqualTo(10);
        await Assert.That(exception.Location.Length).IsEqualTo(2);
    }

    [Test]
    public async Task ValidEscapes_AreStillAcceptedAsync()
    {
        // The guard must not start rejecting the escapes the language does support.
        var tokens = new Lexer("settings {\n    A = \"a\\tb\\nc\\\"d\\\\e\"\n}").Tokenize();

        await Assert.That(tokens.Any(t => t.Type == TokenType.String)).IsTrue();
    }
}
