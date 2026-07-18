namespace Settex.Core.Lexer;

using System.Globalization;
using System.Text;

using Settex.Core.Diagnostics;

/// <summary>
///     Lexer for Settex source code.
///     Converts source text into a stream of tokens.
/// </summary>
public class Lexer(string source, string? filePath = null)
{
    private readonly string source = source;
    private readonly string? filePath = filePath;
    private int position;
    private int line = 1;
    private int column = 1;
    private int bracketDepth; // Track [ ] depth for newline significance

    private char Current => this.position < this.source.Length ? this.source[this.position] : '\0';

    private bool IsAtEnd => this.position >= this.source.Length;

    /// <summary>
    ///     Tokenizes the entire source and returns all tokens.
    /// </summary>
    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        Token token;

        do
        {
            token = this.NextToken();

            // Skip comments, but include everything else
            if (token.Type != TokenType.Comment)
            {
                tokens.Add(token);
            }
        }
        while (token.Type != TokenType.Eof);

        return tokens;
    }

    /// <summary>
    ///     Gets the next token from the source.
    /// </summary>
    public Token NextToken()
    {
        // Skip whitespace (but not newlines in arrays)
        this.SkipWhitespace();

        if (this.IsAtEnd)
        {
            return this.CreateToken(TokenType.Eof, string.Empty);
        }

        var startLine = this.line;
        var startColumn = this.column;

        var ch = this.Current;

        // Comments
        if (ch == '#' || (ch == '/' && this.Peek() == '/'))
        {
            return this.ScanComment(startLine, startColumn);
        }

        // Newlines (significant only inside arrays)
        if (ch == '\n' || ch == '\r')
        {
            return this.ScanNewline(startLine, startColumn);
        }

        // String literals
        if (ch == '"')
        {
            return this.ScanString(startLine, startColumn);
        }

        // Numbers (including negative, but be careful with minus operator)
        if (char.IsDigit(ch))
        {
            return this.ScanNumber(startLine, startColumn);
        }

        // Identifiers and keywords
        if (char.IsLetter(ch) || ch == '_')
        {
            return this.ScanIdentifierOrKeyword(startLine, startColumn);
        }

        // Multi-character and single-character operators/symbols
        return ch switch
        {
            '{' => this.CreateAndAdvance(TokenType.LeftBrace, "{", startLine, startColumn),
            '}' => this.CreateAndAdvance(TokenType.RightBrace, "}", startLine, startColumn),
            '[' => this.ScanLeftBracket(startLine, startColumn),
            ']' => this.ScanRightBracket(startLine, startColumn),
            '(' => this.CreateAndAdvance(TokenType.LeftParen, "(", startLine, startColumn),
            ')' => this.CreateAndAdvance(TokenType.RightParen, ")", startLine, startColumn),
            '.' => this.CreateAndAdvance(TokenType.Dot, ".", startLine, startColumn),
            ',' => this.CreateAndAdvance(TokenType.Comma, ",", startLine, startColumn),
            ';' => this.CreateAndAdvance(TokenType.Semicolon, ";", startLine, startColumn),
            ':' => this.ScanColonEquals(startLine, startColumn),
            '+' => this.CreateAndAdvance(TokenType.Plus, "+", startLine, startColumn),
            '-' => this.ScanMinus(startLine, startColumn),
            '*' => this.CreateAndAdvance(TokenType.Star, "*", startLine, startColumn),
            '/' => this.CreateAndAdvance(TokenType.Slash, "/", startLine, startColumn),
            '<' => this.ScanLessOrLessEqual(startLine, startColumn),
            '>' => this.ScanGreaterOrGreaterEqual(startLine, startColumn),
            '=' => this.ScanEqualsOrEqualEqual(startLine, startColumn),
            '!' => this.ScanNotEqual(startLine, startColumn),
            '?' => this.ScanQuestionQuestion(startLine, startColumn),
            _ => throw new LexerException($"Unexpected character '{ch}'", this.CreateLocation(startLine, startColumn, 1)),
        };
    }

    private char Peek(int offset = 1) => this.position + offset < this.source.Length ? this.source[this.position + offset] : '\0';

    private Token ScanLeftBracket(int startLine, int startColumn)
    {
        this.bracketDepth++;
        return this.CreateAndAdvance(TokenType.LeftBracket, "[", startLine, startColumn);
    }

    private Token ScanRightBracket(int startLine, int startColumn)
    {
        this.bracketDepth--;
        return this.CreateAndAdvance(TokenType.RightBracket, "]", startLine, startColumn);
    }

    private Token ScanComment(int startLine, int startColumn)
    {
        var start = this.position;

        // Skip # or //
        if (this.Current == '#')
        {
            this.Advance();
        }
        else // //
        {
            this.Advance(); // first /
            this.Advance(); // second /
        }

        // Read until end of line or end of file
        while (!this.IsAtEnd && this.Current != '\n' && this.Current != '\r')
        {
            this.Advance();
        }

        var text = this.source.Substring(start, this.position - start);
        return this.CreateToken(TokenType.Comment, text, startLine, startColumn);
    }

    private Token ScanNewline(int startLine, int startColumn)
    {
        var start = this.position;

        // Handle \r\n or \r or \n
        if (this.Current == '\r')
        {
            this.Advance();

            if (this.Current == '\n')
            {
                this.Advance();
            }
        }
        else // \n
        {
            this.Advance();
        }

        var text = this.source.Substring(start, this.position - start);

        // Only emit Newline tokens inside arrays
        if (this.bracketDepth > 0)
        {
            return this.CreateToken(TokenType.Newline, text, startLine, startColumn);
        }

        // Otherwise, continue to next token
        return this.NextToken();
    }

    private Token ScanString(int startLine, int startColumn)
    {
        var start = this.position;
        this.Advance(); // Skip opening "

        var sb = new StringBuilder();

        while (!this.IsAtEnd && this.Current != '"')
        {
            if (this.Current == '\\')
            {
                this.Advance();

                if (this.IsAtEnd)
                {
                    throw new LexerException("Unterminated string literal", this.CreateLocation(startLine, startColumn, this.position - start));
                }

                // Handle escape sequences
                var escaped = this.Current switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => throw new LexerException($"Invalid escape sequence '\\{this.Current}'", this.CreateLocation(this.line, this.column, 2)),
                };

                sb.Append(escaped);
                this.Advance();
            }
            else if (this.Current == '\n' || this.Current == '\r')
            {
                throw new LexerException("Unterminated string literal (newline in string)", this.CreateLocation(startLine, startColumn, this.position - start));
            }
            else
            {
                sb.Append(this.Current);
                this.Advance();
            }
        }

        if (this.IsAtEnd)
        {
            throw new LexerException("Unterminated string literal", this.CreateLocation(startLine, startColumn, this.position - start));
        }

        this.Advance(); // Skip closing "

        var text = this.source.Substring(start, this.position - start);
        var value = sb.ToString();
        return this.CreateToken(TokenType.String, text, startLine, startColumn, value);
    }

    private Token ScanNumber(int startLine, int startColumn)
    {
        var start = this.position;
        var hasDecimal = false;

        // Read digits ('-' is handled by the parser as a unary/binary operator,
        // so a number literal never starts with a sign here).
        while (!this.IsAtEnd && char.IsDigit(this.Current))
        {
            this.Advance();
        }

        // Check for decimal part
        if (this.Current == '.' && char.IsDigit(this.Peek()))
        {
            hasDecimal = true;
            this.Advance(); // Skip .

            while (!this.IsAtEnd && char.IsDigit(this.Current))
            {
                this.Advance();
            }
        }

        var text = this.source.Substring(start, this.position - start);

        if (hasDecimal)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                throw new LexerException($"Invalid float literal '{text}'", this.CreateLocation(startLine, startColumn, text.Length));
            }

            return this.CreateToken(TokenType.Float, text, startLine, startColumn, doubleValue);
        }

        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            throw new LexerException($"Invalid integer literal '{text}'", this.CreateLocation(startLine, startColumn, text.Length));
        }

        return this.CreateToken(TokenType.Integer, text, startLine, startColumn, longValue);
    }

    private Token ScanIdentifierOrKeyword(int startLine, int startColumn)
    {
        var start = this.position;

        // Read [A-Za-z_][A-Za-z0-9_]*
        while (!this.IsAtEnd && (char.IsLetterOrDigit(this.Current) || this.Current == '_'))
        {
            this.Advance();
        }

        var text = this.source.Substring(start, this.position - start);

        // Check for keywords
        var type = text switch
        {
            "settings" => TokenType.Settings,
            "env" => TokenType.Env,
            "true" => TokenType.True,
            "false" => TokenType.False,
            "null" => TokenType.Null,
            "include" => TokenType.Include,
            "let" => TokenType.Let,
            "and" => TokenType.And,
            "or" => TokenType.Or,
            "not" => TokenType.Not,
            "if" => TokenType.If,
            "for" => TokenType.For,
            "in" => TokenType.In,
            _ => TokenType.Identifier,
        };

        // For true/false/null, also set the Value
        object? value = type switch
        {
            TokenType.True => true,
            TokenType.False => false,
            _ => null,
        };

        return this.CreateToken(type, text, startLine, startColumn, value);
    }

    private void SkipWhitespace()
    {
        while (!this.IsAtEnd)
        {
            var ch = this.Current;

            // Newlines are handled separately (they might be significant)
            if (ch == '\n' || ch == '\r')
            {
                break;
            }

            // Skip spaces and tabs
            if (ch == ' ' || ch == '\t')
            {
                this.Advance();
            }
            else
            {
                break;
            }
        }
    }

    private Token ScanMinus(int startLine, int startColumn)
    {
        // '-' is always the minus operator. The parser turns a leading '-' into a
        // unary negation (ParseUnary) and an infix '-' into subtraction (ParseTerm).
        // Lexing '-3' as a single negative literal would break subtraction written
        // without surrounding spaces (e.g. '5-3'), so we never do that here.
        return this.CreateAndAdvance(TokenType.Minus, "-", startLine, startColumn);
    }

    private Token ScanLessOrLessEqual(int startLine, int startColumn)
    {
        this.Advance(); // consume <

        if (this.Current == '=')
        {
            this.Advance(); // consume =
            return this.CreateToken(TokenType.LessEqual, "<=", startLine, startColumn);
        }

        return this.CreateToken(TokenType.Less, "<", startLine, startColumn);
    }

    private Token ScanGreaterOrGreaterEqual(int startLine, int startColumn)
    {
        this.Advance(); // consume >

        if (this.Current == '=')
        {
            this.Advance(); // consume =
            return this.CreateToken(TokenType.GreaterEqual, ">=", startLine, startColumn);
        }

        return this.CreateToken(TokenType.Greater, ">", startLine, startColumn);
    }

    private Token ScanEqualsOrEqualEqual(int startLine, int startColumn)
    {
        this.Advance(); // consume =

        if (this.Current == '=')
        {
            this.Advance(); // consume second =
            return this.CreateToken(TokenType.EqualEqual, "==", startLine, startColumn);
        }

        return this.CreateToken(TokenType.Equals, "=", startLine, startColumn);
    }

    private Token ScanNotEqual(int startLine, int startColumn)
    {
        this.Advance(); // consume !

        if (this.Current == '=')
        {
            this.Advance(); // consume =
            return this.CreateToken(TokenType.NotEqual, "!=", startLine, startColumn);
        }

        throw new LexerException("Unexpected character '!'. Did you mean '!='?", this.CreateLocation(startLine, startColumn, 1));
    }

    private Token ScanQuestionQuestion(int startLine, int startColumn)
    {
        this.Advance(); // consume first ?

        if (this.Current == '?')
        {
            this.Advance(); // consume second ?
            return this.CreateToken(TokenType.QuestionQuestion, "??", startLine, startColumn);
        }

        throw new LexerException("Unexpected character '?'. Did you mean '??'?", this.CreateLocation(startLine, startColumn, 1));
    }

    private Token ScanColonEquals(int startLine, int startColumn)
    {
        this.Advance(); // consume :

        if (this.Current == '=')
        {
            this.Advance(); // consume =
            return this.CreateToken(TokenType.ColonEquals, ":=", startLine, startColumn);
        }

        throw new LexerException("Unexpected character ':'. Did you mean ':='?", this.CreateLocation(startLine, startColumn, 1));
    }

    private void Advance()
    {
        if (!this.IsAtEnd)
        {
            if (this.Current == '\n')
            {
                this.line++;
                this.column = 1;
            }
            else
            {
                this.column++;
            }

            this.position++;
        }
    }

    private Token CreateAndAdvance(TokenType type, string text, int lineParam, int columnParam)
    {
        var token = this.CreateToken(type, text, lineParam, columnParam);
        this.Advance();
        return token;
    }

    private Token CreateToken(TokenType type, string text, int? lineParam = null, int? columnParam = null, object? value = null)
        => new()
        {
            Type = type,
            Text = text,
            Location = this.CreateLocation(lineParam ?? this.line, columnParam ?? this.column, text.Length),
            Value = value,
        };

    private SourceLocation CreateLocation(int lineParam, int columnParam, int length)
        => new()
        {
            FilePath = this.filePath,
            Line = lineParam,
            Column = columnParam,
            Length = length,
        };
}
