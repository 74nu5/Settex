namespace Settex.Core.Tests.Lexer;

using Settex.Core.Lexer;

public sealed class LexerTests
{
    [Test]
    public async Task Tokenize_EmptySource_ReturnsEof()
    {
        // Arrange
        var lexer = new Lexer("");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens).Count().IsEqualTo(1);
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.Eof);
    }

    [Test]
    public async Task Tokenize_Keywords_ReturnsCorrectTokens()
    {
        // Arrange
        var lexer = new Lexer("settings env true false null include");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens).Count().IsEqualTo(7); // 6 keywords + EOF
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.Settings);
        await Assert.That(tokens[1].Type).IsEqualTo(TokenType.Env);
        await Assert.That(tokens[2].Type).IsEqualTo(TokenType.True);
        await Assert.That(tokens[3].Type).IsEqualTo(TokenType.False);
        await Assert.That(tokens[4].Type).IsEqualTo(TokenType.Null);
        await Assert.That(tokens[5].Type).IsEqualTo(TokenType.Include);
        await Assert.That(tokens[6].Type).IsEqualTo(TokenType.Eof);
    }

    [Test]
    public async Task Tokenize_Identifiers_ReturnsIdentifierTokens()
    {
        // Arrange
        var lexer = new Lexer("foo _bar Baz123 __test__");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens).Count().IsEqualTo(5); // 4 identifiers + EOF
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.Identifier);
        await Assert.That(tokens[0].Text).IsEqualTo("foo");
        await Assert.That(tokens[1].Text).IsEqualTo("_bar");
        await Assert.That(tokens[2].Text).IsEqualTo("Baz123");
        await Assert.That(tokens[3].Text).IsEqualTo("__test__");
    }

    [Test]
    public async Task Tokenize_Symbols_ReturnsCorrectTokens()
    {
        // Arrange
        var lexer = new Lexer("{ } [ ] = . , ;");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens).Count().IsEqualTo(9); // 8 symbols + EOF
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.LeftBrace);
        await Assert.That(tokens[1].Type).IsEqualTo(TokenType.RightBrace);
        await Assert.That(tokens[2].Type).IsEqualTo(TokenType.LeftBracket);
        await Assert.That(tokens[3].Type).IsEqualTo(TokenType.RightBracket);
        await Assert.That(tokens[4].Type).IsEqualTo(TokenType.Equals);
        await Assert.That(tokens[5].Type).IsEqualTo(TokenType.Dot);
        await Assert.That(tokens[6].Type).IsEqualTo(TokenType.Comma);
        await Assert.That(tokens[7].Type).IsEqualTo(TokenType.Semicolon);
    }

    [Test]
    public async Task Tokenize_String_Simple_ReturnsStringToken()
    {
        // Arrange
        var lexer = new Lexer("\"hello\"");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens).Count().IsEqualTo(2); // String + EOF
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.String);
        await Assert.That(tokens[0].Value).IsEqualTo("hello");
    }

    [Test]
    public async Task Tokenize_String_WithEscapes_ReturnsUnescapedValue()
    {
        // Arrange
        var lexer = new Lexer("\"line\\nbreak\" \"tab\\there\" \"quote\\\"inside\"");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens).Count().IsEqualTo(4); // 3 strings + EOF
        await Assert.That(tokens[0].Value).IsEqualTo("line\nbreak");
        await Assert.That(tokens[1].Value).IsEqualTo("tab\there");
        await Assert.That(tokens[2].Value).IsEqualTo("quote\"inside");
    }

    [Test]
    public async Task Tokenize_String_Empty_ReturnsEmptyString()
    {
        // Arrange
        var lexer = new Lexer("\"\"");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.String);
        await Assert.That(tokens[0].Value).IsEqualTo("");
    }

    [Test]
    public async Task Tokenize_String_Unterminated_ThrowsException()
    {
        // Arrange
        var lexer = new Lexer("\"unterminated");

        // Act & Assert
        await Assert.ThrowsAsync<LexerException>(() => Task.FromResult(lexer.Tokenize()));
    }

    [Test]
    public async Task Tokenize_Integer_Positive_ReturnsIntegerToken()
    {
        // Arrange
        var lexer = new Lexer("42 0 123");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens).Count().IsEqualTo(4); // 3 integers + EOF
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.Integer);
        await Assert.That(tokens[0].Value).IsEqualTo(42L);
        await Assert.That(tokens[1].Value).IsEqualTo(0L);
        await Assert.That(tokens[2].Value).IsEqualTo(123L);
    }

    [Test]
    public async Task Tokenize_MinusBeforeNumber_ProducesMinusOperatorThenNumber()
    {
        // '-' is always a minus operator token; negation is handled by the parser.
        // Lexing "-42" as a single negative literal would break subtraction without
        // spaces (e.g. "5-3"), so the lexer never does that.
        var lexer = new Lexer("-42 -17");

        var tokens = lexer.Tokenize();

        await Assert.That(tokens).Count().IsEqualTo(5); // - 42 - 17 EOF
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.Minus);
        await Assert.That(tokens[1].Type).IsEqualTo(TokenType.Integer);
        await Assert.That(tokens[1].Value).IsEqualTo(42L);
        await Assert.That(tokens[2].Type).IsEqualTo(TokenType.Minus);
        await Assert.That(tokens[3].Value).IsEqualTo(17L);
    }

    [Test]
    public async Task Tokenize_Subtraction_WithoutSpaces_ProducesOperator()
    {
        // Regression: "5-3" must lex to 5, '-', 3 (not 5 then negative-3).
        var tokens = new Lexer("5-3").Tokenize();

        await Assert.That(tokens).Count().IsEqualTo(4); // 5 - 3 EOF
        await Assert.That(tokens[0].Value).IsEqualTo(5L);
        await Assert.That(tokens[1].Type).IsEqualTo(TokenType.Minus);
        await Assert.That(tokens[2].Value).IsEqualTo(3L);
    }

    [Test]
    public async Task Tokenize_Float_ReturnsFloatToken()
    {
        // Arrange
        var lexer = new Lexer("3.14 -0.5 0.0");

        // Act
        var tokens = lexer.Tokenize();

        // Assert — '-' is a separate operator token (negation handled by the parser).
        await Assert.That(tokens).Count().IsEqualTo(5); // 3.14 - 0.5 0.0 EOF
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.Float);
        await Assert.That(tokens[0].Value).IsEqualTo(3.14);
        await Assert.That(tokens[1].Type).IsEqualTo(TokenType.Minus);
        await Assert.That(tokens[2].Type).IsEqualTo(TokenType.Float);
        await Assert.That(tokens[2].Value).IsEqualTo(0.5);
        await Assert.That(tokens[3].Value).IsEqualTo(0.0);
    }

    [Test]
    public async Task Tokenize_Comments_Hash_AreSkipped()
    {
        // Arrange
        var lexer = new Lexer("foo # this is a comment\nbar");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens).Count().IsEqualTo(3); // foo, bar, EOF (comment skipped)
        await Assert.That(tokens[0].Text).IsEqualTo("foo");
        await Assert.That(tokens[1].Text).IsEqualTo("bar");
    }

    [Test]
    public async Task Tokenize_Comments_DoubleSlash_AreSkipped()
    {
        // Arrange
        var lexer = new Lexer("foo // this is a comment\nbar");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens).Count().IsEqualTo(3); // foo, bar, EOF
        await Assert.That(tokens[0].Text).IsEqualTo("foo");
        await Assert.That(tokens[1].Text).IsEqualTo("bar");
    }

    [Test]
    public async Task Tokenize_Newlines_OutsideArrays_AreSkipped()
    {
        // Arrange
        var lexer = new Lexer("foo\nbar\n\nbaz");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens).Count().IsEqualTo(4); // foo, bar, baz, EOF (no newlines)
        await Assert.That(tokens.Any(t => t.Type == TokenType.Newline)).IsFalse();
    }

    [Test]
    public async Task Tokenize_Newlines_InsideArrays_AreIncluded()
    {
        // Arrange
        var lexer = new Lexer("[1\n2\n3]");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        // [ 1 \n 2 \n 3 ] EOF
        await Assert.That(tokens).Count().IsEqualTo(8);
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.LeftBracket);
        await Assert.That(tokens[1].Type).IsEqualTo(TokenType.Integer);
        await Assert.That(tokens[2].Type).IsEqualTo(TokenType.Newline);
        await Assert.That(tokens[3].Type).IsEqualTo(TokenType.Integer);
        await Assert.That(tokens[4].Type).IsEqualTo(TokenType.Newline);
        await Assert.That(tokens[5].Type).IsEqualTo(TokenType.Integer);
        await Assert.That(tokens[6].Type).IsEqualTo(TokenType.RightBracket);
    }

    [Test]
    public async Task Tokenize_Location_IsCorrect()
    {
        // Arrange
        var lexer = new Lexer("foo\nbar", "test.settex");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        await Assert.That(tokens[0].Location.Line).IsEqualTo(1);
        await Assert.That(tokens[0].Location.Column).IsEqualTo(1);
        await Assert.That(tokens[0].Location.FilePath).IsEqualTo("test.settex");

        await Assert.That(tokens[1].Location.Line).IsEqualTo(2);
        await Assert.That(tokens[1].Location.Column).IsEqualTo(1);
    }

    [Test]
    public async Task Tokenize_ComplexExample_ReturnsCorrectTokens()
    {
        // Arrange
        var source = """
                     settings {
                       Server.Host = "localhost"
                       Port = 8080
                     }
                     """;

        var lexer = new Lexer(source);

        // Act
        var tokens = lexer.Tokenize();

        // Assert - should have: settings { Server . Host = "localhost" Port = 8080 } EOF
        await Assert.That(tokens).Count().IsEqualTo(12);
        await Assert.That(tokens[0].Type).IsEqualTo(TokenType.Settings);
        await Assert.That(tokens[1].Type).IsEqualTo(TokenType.LeftBrace);
        await Assert.That(tokens[2].Type).IsEqualTo(TokenType.Identifier); // Server
        await Assert.That(tokens[3].Type).IsEqualTo(TokenType.Dot);
        await Assert.That(tokens[4].Type).IsEqualTo(TokenType.Identifier); // Host
        await Assert.That(tokens[5].Type).IsEqualTo(TokenType.Equals);
        await Assert.That(tokens[6].Type).IsEqualTo(TokenType.String);
        await Assert.That(tokens[7].Type).IsEqualTo(TokenType.Identifier); // Port
        await Assert.That(tokens[8].Type).IsEqualTo(TokenType.Equals);
        await Assert.That(tokens[9].Type).IsEqualTo(TokenType.Integer);
        await Assert.That(tokens[10].Type).IsEqualTo(TokenType.RightBrace);
        await Assert.That(tokens[11].Type).IsEqualTo(TokenType.Eof);
    }
}
