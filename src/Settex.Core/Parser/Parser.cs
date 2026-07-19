namespace Settex.Core.Parser;

using Settex.Core.Diagnostics;
using Settex.Core.Lexer;
using Settex.Core.Parser.Ast;

/// <summary>
///     Recursive descent parser for Settex source code.
///     Converts a stream of tokens into an Abstract Syntax Tree (AST).
/// </summary>
public class Parser(List<Token> tokens, string? filePath = null)
{
    private readonly List<Token> tokens = tokens;
    private readonly string? filePath = filePath;
    private int position;

    private Token Current => this.position < this.tokens.Count ? this.tokens[this.position] : this.tokens[^1];
    
    private Token Previous => this.position > 0 ? this.tokens[this.position - 1] : this.tokens[0];

    private bool IsAtEnd => this.Current.Type == TokenType.Eof;

    /// <summary>
    ///     Parses the entire file and returns the root FileNode.
    /// </summary>
    public FileNode Parse()
    {
        var startLocation = this.Current.Location;
        var statements = new List<ITopLevelStatement>();

        while (!this.IsAtEnd)
        {
            var stmt = this.ParseTopLevelStatement();

            if (stmt != null)
            {
                statements.Add(stmt);
            }
        }

        var eofToken = this.Expect(TokenType.Eof, "Expected end of file");

        return new(statements, SpanTo(startLocation, eofToken));
    }

    /// <summary>
    ///     Alias for Parse() - for backward compatibility.
    /// </summary>
    public FileNode ParseFile() => this.Parse();

    private Token Peek(int offset = 1) => this.position + offset < this.tokens.Count ? this.tokens[this.position + offset] : this.tokens[^1];

    /// <summary>
    ///     Extends a start location so it spans up to and including <paramref name="endToken" />
    ///     (typically the block's closing brace). Gives multi-line nodes a real end,
    ///     which editors need for symbol ranges and scope resolution.
    /// </summary>
    private static SourceLocation SpanTo(SourceLocation start, Token endToken)
        => start with
        {
            EndLine = endToken.Location.Line,
            EndColumn = endToken.Location.Column + endToken.Location.Length,
        };

    /// <summary>
    ///     Extends a start location so it spans up to the end of <paramref name="end" />.
    /// </summary>
    private static SourceLocation SpanTo(SourceLocation start, SourceLocation end)
        => start with
        {
            EndLine = end.EffectiveEndLine,
            EndColumn = end.EffectiveEndColumn,
        };

    /// <summary>
    ///     Parses a top-level statement: settingsBlock | envBlock | includeStmt | letStmt | ";"
    /// </summary>
    private ITopLevelStatement? ParseTopLevelStatement()
    {
        // Skip semicolons
        if (this.Match(TokenType.Semicolon))
        {
            return null;
        }

        if (this.Check(TokenType.Include))
        {
            return this.ParseIncludeStatement();
        }

        if (this.Check(TokenType.Let))
        {
            return this.ParseLetStatement();
        }

        if (this.Check(TokenType.Settings))
        {
            return this.ParseSettingsBlock();
        }

        if (this.Check(TokenType.Env))
        {
            return this.ParseEnvBlock();
        }

        throw new ParserException(
            $"Expected 'include', 'let', 'settings' or 'env', but got '{this.Current.Text}'",
            this.Current.Location
        );
    }

    /// <summary>
    ///     Parses an include statement: "include" string
    /// </summary>
    private IncludeNode ParseIncludeStatement()
    {
        var includeToken = this.Expect(TokenType.Include, "Expected 'include'");
        var pathToken = this.Expect(TokenType.String, "Expected include path string");

        var includePath = (string)pathToken.Value!;

        return new(includePath, includeToken.Location);
    }

    /// <summary>
    ///     Parses a let statement: "let" ident "=" expression
    /// </summary>
    private LetNode ParseLetStatement()
    {
        var letToken = this.Expect(TokenType.Let, "Expected 'let'");
        var nameToken = this.Expect(TokenType.Identifier, "Expected variable name");

        this.Expect(TokenType.Equals, "Expected '=' after variable name");

        // Check for tagged object value (e.g., let obj = svc { ... })
        IExpression value;
        if (this.Check(TokenType.Identifier) && this.Peek().Type == TokenType.LeftBrace)
        {
            value = this.ParseTaggedObjectValue();
        }
        else
        {
            value = this.ParseExpression();
        }

        return new(nameToken.Text, value, letToken.Location);
    }

    /// <summary>
    ///     Parses an expression with operator precedence (Pratt parser).
    ///     Entry point for expression parsing.
    /// </summary>
    private IExpression ParseExpression()
    {
        return this.ParseLogicalOr();
    }

    // Precedence levels (lowest to highest):
    // 1. Logical OR
    // 2. Logical AND
    // 3. Null coalescing (??)
    // 4. Equality (==, !=)
    // 5. Comparison (<, <=, >, >=)
    // 6. Term (+, -)
    // 7. Factor (*, /)
    // 8. Unary (not, -)
    // 9. Primary (literals, variables, arrays, objects, parentheses)

    private IExpression ParseLogicalOr()
    {
        var left = this.ParseLogicalAnd();

        while (this.Match(TokenType.Or))
        {
            var opToken = this.Previous;
            var right = this.ParseLogicalAnd();
            left = new BinaryOpNode(left, "or", right, opToken.Location);
        }

        return left;
    }

    private IExpression ParseLogicalAnd()
    {
        var left = this.ParseCoalesce();

        while (this.Match(TokenType.And))
        {
            var opToken = this.Previous;
            var right = this.ParseCoalesce();
            left = new BinaryOpNode(left, "and", right, opToken.Location);
        }

        return left;
    }

    private IExpression ParseCoalesce()
    {
        var left = this.ParseEquality();

        while (this.Match(TokenType.QuestionQuestion))
        {
            var opToken = this.Previous;
            var right = this.ParseEquality();
            left = new BinaryOpNode(left, "??", right, opToken.Location);
        }

        return left;
    }

    private IExpression ParseEquality()
    {
        var left = this.ParseComparison();

        while (this.Match(TokenType.EqualEqual, TokenType.NotEqual))
        {
            var opToken = this.Previous;
            var op = opToken.Type == TokenType.EqualEqual ? "==" : "!=";
            var right = this.ParseComparison();
            left = new BinaryOpNode(left, op, right, opToken.Location);
        }

        return left;
    }

    private IExpression ParseComparison()
    {
        var left = this.ParseTerm();

        while (this.Match(TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual))
        {
            var opToken = this.Previous;
            var op = opToken.Type switch
            {
                TokenType.Less => "<",
                TokenType.LessEqual => "<=",
                TokenType.Greater => ">",
                TokenType.GreaterEqual => ">=",
                _ => throw new ParserException($"Unexpected comparison operator: {opToken.Type}", opToken.Location),
            };
            var right = this.ParseTerm();
            left = new BinaryOpNode(left, op, right, opToken.Location);
        }

        return left;
    }

    private IExpression ParseTerm()
    {
        var left = this.ParseFactor();

        while (this.Match(TokenType.Plus, TokenType.Minus))
        {
            var opToken = this.Previous;
            var op = opToken.Type == TokenType.Plus ? "+" : "-";
            var right = this.ParseFactor();
            left = new BinaryOpNode(left, op, right, opToken.Location);
        }

        return left;
    }

    private IExpression ParseFactor()
    {
        var left = this.ParseUnary();

        while (this.Match(TokenType.Star, TokenType.Slash))
        {
            var opToken = this.Previous;
            var op = opToken.Type == TokenType.Star ? "*" : "/";
            var right = this.ParseUnary();
            left = new BinaryOpNode(left, op, right, opToken.Location);
        }

        return left;
    }

    private IExpression ParseUnary()
    {
        if (this.Match(TokenType.Not))
        {
            var opToken = this.Previous;
            var operand = this.ParseUnary();
            return new UnaryOpNode("not", operand, opToken.Location);
        }

        if (this.Match(TokenType.Minus))
        {
            var opToken = this.Previous;
            var operand = this.ParseUnary();
            return new UnaryOpNode("-", operand, opToken.Location);
        }

        return this.ParsePrimary();
    }

    private IExpression ParsePrimary()
    {
        // Check for variable reference or keyword usable as identifier
        if (this.Check(TokenType.Identifier) || this.IsKeywordUsableAsIdentifier())
        {
            var nameToken = this.Current;
            this.Advance();
            IExpression expr = new VariableRefNode(nameToken.Text, nameToken.Location);
            
            // Check for member access (e.g., user.Name, server.Port)
            while (this.Check(TokenType.Dot))
            {
                this.Advance(); // consume '.'
                var memberToken = this.Expect(TokenType.Identifier, "Expected member name after '.'");
                expr = new MemberAccessNode(expr, memberToken.Text, nameToken.Location);
            }
            
            return expr;
        }

        // Literal values (including interpolated strings)
        if (this.Check(TokenType.String) || this.Check(TokenType.Integer) ||
            this.Check(TokenType.Float) || this.Check(TokenType.True) ||
            this.Check(TokenType.False) || this.Check(TokenType.Null))
        {
            return this.ParseLiteral();
        }

        // Array
        if (this.Check(TokenType.LeftBracket))
        {
            return this.ParseArray();
        }

        // Parenthesized (grouped) expression
        if (this.Check(TokenType.LeftParen))
        {
            this.Advance(); // consume '('
            var inner = this.ParseExpression();
            this.Expect(TokenType.RightParen, "Expected ')' after expression");
            return inner;
        }

        throw new ParserException(
            $"Expected expression, but got '{this.Current.Text}'",
            this.Current.Location
        );
    }

    /// <summary>
    ///     Checks if the current token is a keyword that can be used as an identifier in expressions.
    ///     For example, 'env' is a keyword for env blocks but can also be used as a variable name.
    /// </summary>
    private bool IsKeywordUsableAsIdentifier()
    {
        // Only 'env' for now - it's used as an implicit variable but is also a keyword
        return this.Current.Type == TokenType.Env;
    }

    /// <summary>
    ///     Parses a settings block: "settings" block
    /// </summary>
    private SettingsBlockNode ParseSettingsBlock()
    {
        var settingsToken = this.Expect(TokenType.Settings, "Expected 'settings'");
        var block = this.ParseBlock();

        return new(block, SpanTo(settingsToken.Location, block.Location));
    }

    /// <summary>
    ///     Parses an env block: "env" string "{" [let]* settingsBlock "}"
    ///     In V2, env blocks can have let statements before the settings block.
    /// </summary>
    private EnvBlockNode ParseEnvBlock()
    {
        var envToken = this.Expect(TokenType.Env, "Expected 'env'");
        var nameToken = this.Expect(TokenType.String, "Expected environment name string");

        var envName = (string)nameToken.Value!;

        this.Expect(TokenType.LeftBrace, "Expected '{' after environment name");

        // Skip newlines
        while (this.Match(TokenType.Newline))
        {
        }

        // Parse let statements (V2 feature)
        var letStatements = new List<LetNode>();
        while (this.Check(TokenType.Let))
        {
            var letStmt = this.ParseLetStatement();
            letStatements.Add(letStmt);

            // Skip newlines after let statement
            while (this.Match(TokenType.Newline))
            {
            }
        }

        // Parse settings block
        var settingsBlock = this.ParseSettingsBlock();

        var envCloseToken = this.Expect(TokenType.RightBrace, "Expected '}' to close env block");

        // For now, we store let statements in the EnvBlockNode's SettingsBlock
        // This is a simplification - we'll refactor when we implement proper scoping
        if (letStatements.Count > 0)
        {
            // Prepend let statements to the settings block's statements
            var allStatements = new List<IStatement>();
            allStatements.AddRange(letStatements);
            allStatements.AddRange(settingsBlock.Block.Statements);

            var newBlock = new BlockNode(allStatements, settingsBlock.Block.Location);
            settingsBlock = new SettingsBlockNode(newBlock, settingsBlock.Location);
        }

        return new(envName, settingsBlock, SpanTo(envToken.Location, envCloseToken));
    }

    /// <summary>
    ///     Parses a block: "{" stmt* "}"
    /// </summary>
    private BlockNode ParseBlock()
    {
        var startToken = this.Expect(TokenType.LeftBrace, "Expected '{'");
        var statements = new List<IStatement>();

        while (!this.Check(TokenType.RightBrace) && !this.IsAtEnd)
        {
            // Skip newlines at statement level (only significant inside arrays)
            if (this.Match(TokenType.Newline))
            {
                continue;
            }

            var stmt = this.ParseStatement();

            if (stmt != null)
            {
                statements.Add(stmt);
            }
        }

        var closeToken = this.Expect(TokenType.RightBrace, "Expected '}'");

        return new(statements, SpanTo(startToken.Location, closeToken));
    }

    /// <summary>
    ///     Parses a statement: letStmt | assignStmt | nestedBlockStmt | ";"
    /// </summary>
    private IStatement? ParseStatement()
    {
        // Skip semicolons
        if (this.Match(TokenType.Semicolon))
        {
            return null;
        }

        // Check for let statement
        if (this.Check(TokenType.Let))
        {
            return this.ParseLetStatement();
        }

        // Look ahead to distinguish between assignment and nested block
        // If we see: Ident "=" → assignment
        // If we see: Ident ":=" → assignment
        // If we see: Ident "." → assignment (path)
        // If we see: Ident "{" → nested block
        if (this.Check(TokenType.Identifier))
        {
            if (this.Peek().Type == TokenType.Equals || this.Peek().Type == TokenType.ColonEquals || this.Peek().Type == TokenType.Dot)
            {
                return this.ParseAssignmentStatement();
            }

            if (this.Peek().Type == TokenType.LeftBrace)
            {
                return this.ParseNestedBlockStatement();
            }
        }

        throw new ParserException(
            $"Expected 'let', identifier for assignment or nested block, but got '{this.Current.Text}'",
            this.Current.Location
        );
    }

    /// <summary>
    ///     Parses an assignment statement: path ("=" | ":=") value/expression ["if" condition]
    /// </summary>
    private AssignmentNode ParseAssignmentStatement()
    {
        var path = this.ParsePath();
        
        // Determine assignment operator
        var op = AssignmentOp.Set;
        
        if (this.Match(TokenType.ColonEquals))
        {
            op = AssignmentOp.SetIfMissing;
        }
        else if (this.Match(TokenType.Equals))
        {
            op = AssignmentOp.Set;
        }
        else
        {
            throw new ParserException($"Expected '=' or ':=' after path, but got '{this.Current.Text}'", this.Current.Location);
        }
        
        // Parse value - can be expression or tagged object
        // Check for tagged object first: ident {
        IExpression value;
        if (this.Check(TokenType.Identifier) && this.Peek().Type == TokenType.LeftBrace)
        {
            value = this.ParseTaggedObjectValue();
        }
        else
        {
            value = this.ParseExpression();
        }

        // Check for optional "if" condition
        IExpression? condition = null;

        if (this.Match(TokenType.If))
        {
            condition = this.ParseExpression();
        }

        return new(path, op, value, condition, path.Location);
    }

    /// <summary>
    ///     Parses a nested block statement: ident block
    /// </summary>
    private NestedBlockNode ParseNestedBlockStatement()
    {
        var identToken = this.Expect(TokenType.Identifier, "Expected identifier");
        var block = this.ParseBlock();

        return new(identToken.Text, block, SpanTo(identToken.Location, block.Location));
    }

    /// <summary>
    ///     Parses a path: ident ("." ident)*
    /// </summary>
    private PathNode ParsePath()
    {
        var segments = new List<string>();
        var startToken = this.Expect(TokenType.Identifier, "Expected identifier");
        segments.Add(startToken.Text);

        while (this.Match(TokenType.Dot))
        {
            var identToken = this.Expect(TokenType.Identifier, "Expected identifier after '.'");
            segments.Add(identToken.Text);
        }

        return new(segments, startToken.Location);
    }

    /// <summary>
    ///     Parses a value: literal | array | taggedObjectValue
    /// </summary>
    private IExpression ParseValue()
    {
        // Literal values (including interpolated strings)
        if (this.Check(TokenType.String) || this.Check(TokenType.Integer) ||
            this.Check(TokenType.Float) || this.Check(TokenType.True) ||
            this.Check(TokenType.False) || this.Check(TokenType.Null))
        {
            return this.ParseLiteral();
        }

        // Array
        if (this.Check(TokenType.LeftBracket))
        {
            return this.ParseArray();
        }

        // Tagged object value: ident block
        if (this.Check(TokenType.Identifier))
        {
            if (this.Peek().Type == TokenType.LeftBrace)
            {
                return this.ParseTaggedObjectValue();
            }
        }

        throw new ParserException(
            $"Expected value (literal, array, or tagged object), but got '{this.Current.Text}'",
            this.Current.Location
        );
    }

    /// <summary>
    ///     Parses a literal: string | number | "true" | "false" | "null"
    ///     For strings, detects interpolation ${...} and creates InterpolatedStringNode if needed.
    /// </summary>
    private IExpression ParseLiteral()
    {
        var token = this.Current;
        this.Advance();

        // Check if it's a string with interpolation
        if (token.Type == TokenType.String && token.Value is string str && str.Contains("${"))
        {
            return this.ParseInterpolatedString(str, token.Location);
        }

        return new LiteralNode(token.Value, token.Location);
    }

    /// <summary>
    ///     Parses an interpolated string: "text ${expr} text".
    ///     The input is the decoded string value with ${...} segments.
    /// </summary>
    private InterpolatedStringNode ParseInterpolatedString(string str, SourceLocation location)
    {
        var segments = new List<StringSegment>();
        var currentPos = 0;

        while (currentPos < str.Length)
        {
            var dollarPos = str.IndexOf("${", currentPos);

            if (dollarPos == -1)
            {
                // No more interpolations, add remaining as literal
                if (currentPos < str.Length)
                {
                    segments.Add(new LiteralSegment(str.Substring(currentPos)));
                }

                break;
            }

            // Add literal segment before ${
            if (dollarPos > currentPos)
            {
                segments.Add(new LiteralSegment(str.Substring(currentPos, dollarPos - currentPos)));
            }

            // Find matching }
            var braceCount = 1;
            var exprStart = dollarPos + 2; // Skip ${
            var exprPos = exprStart;

            while (exprPos < str.Length && braceCount > 0)
            {
                if (str[exprPos] == '{')
                {
                    braceCount++;
                }
                else if (str[exprPos] == '}')
                {
                    braceCount--;
                }

                exprPos++;
            }

            if (braceCount != 0)
            {
                throw new ParserException("Unterminated interpolation expression ${...}", location);
            }

            // Extract expression source (between ${ and })
            var exprSource = str.Substring(exprStart, exprPos - exprStart - 1);

            // Parse the embedded expression. The sub-tokens' positions are relative
            // to exprSource, so any lex/parse error is re-anchored to the interpolated
            // string's location in the real file rather than reported at a phantom
            // line 1 / column 1.
            IExpression expr;
            Parser exprParser;

            try
            {
                var exprLexer = new Lexer(exprSource, this.filePath);
                var exprTokens = exprLexer.Tokenize();
                exprParser = new Parser(exprTokens, this.filePath);
                expr = exprParser.ParseExpression();
            }
            catch (Exception ex) when (ex is ParserException or LexerException)
            {
                throw new ParserException(
                    $"Invalid interpolation expression '${{{exprSource.Trim()}}}': {ex.Message}",
                    location);
            }

            // The whole ${...} must be exactly one expression. Reject leftover tokens
            // (e.g. "${a b}") instead of silently ignoring everything after the first.
            if (!exprParser.IsAtEnd)
            {
                throw new ParserException(
                    $"Invalid interpolation expression '${{{exprSource.Trim()}}}': unexpected '{exprParser.Current.Text}'",
                    location);
            }

            segments.Add(new ExpressionSegment(expr));

            currentPos = exprPos;
        }

        return new InterpolatedStringNode(segments, location);
    }

    /// <summary>
    ///     Parses an array: "[" arrayItems? "]"
    /// </summary>
    private ArrayNode ParseArray()
    {
        var startToken = this.Expect(TokenType.LeftBracket, "Expected '['");
        var elements = new List<IArrayElement>();

        // Skip leading newlines
        while (this.Match(TokenType.Newline))
        {
        }

        // Empty array
        if (this.Check(TokenType.RightBracket))
        {
            var emptyCloseToken = this.Current;
            this.Advance();
            return new(elements, SpanTo(startToken.Location, emptyCloseToken));
        }

        // Parse array items with comma or newline separators
        elements.Add(this.ParseArrayItem());

        while (!this.Check(TokenType.RightBracket) && !this.IsAtEnd)
        {
            // Accept comma or newline as separator
            if (this.Match(TokenType.Comma) || this.Match(TokenType.Newline))
            {
                // Skip multiple separators
                while (this.Match(TokenType.Comma) || this.Match(TokenType.Newline))
                {
                }

                // Check for trailing separator
                if (this.Check(TokenType.RightBracket))
                {
                    break;
                }

                elements.Add(this.ParseArrayItem());
            }
            else
            {
                throw new ParserException(
                    "Expected ',' or newline between array items",
                    this.Current.Location
                );
            }
        }

        var arrayCloseToken = this.Expect(TokenType.RightBracket, "Expected ']' to close array");

        return new(elements, SpanTo(startToken.Location, arrayCloseToken));
    }

    /// <summary>
    ///     Parses an array item: expression (literal, variable ref, binary op, etc.), tagged object, or for loop
    /// </summary>
    private IArrayElement ParseArrayItem()
    {
        // Check for for loop
        if (this.Check(TokenType.For))
        {
            return this.ParseForLoop();
        }
        
        // Check for tagged object: ident {
        if (this.Check(TokenType.Identifier) && this.Peek().Type == TokenType.LeftBrace)
        {
            return this.ParseTaggedObjectValue();
        }
        
        // Otherwise, parse as expression (literal, variable, binary op, etc.)
        return this.ParseExpression();
    }

    /// <summary>
    ///     Parses a for loop: for ident in expr { block }
    /// </summary>
    private ForNode ParseForLoop()
    {
        var startToken = this.Expect(TokenType.For, "Expected 'for'");
        var iteratorToken = this.Expect(TokenType.Identifier, "Expected iterator variable name after 'for'");
        this.Expect(TokenType.In, "Expected 'in' after iterator variable");
        
        var collection = this.ParseExpression();
        
        // Skip newlines before block
        while (this.Match(TokenType.Newline))
        {
        }
        
        var body = this.ParseBlock();
        
        return new(iteratorToken.Text, collection, body, SpanTo(startToken.Location, body.Location));
    }

    /// <summary>
    ///     Parses a tagged object value: ident block
    /// </summary>
    private TaggedObjectNode ParseTaggedObjectValue()
    {
        var identToken = this.Expect(TokenType.Identifier, "Expected identifier");
        var block = this.ParseBlock();

        return new(identToken.Text, block, SpanTo(identToken.Location, block.Location));
    }

    // Helper methods

    private bool Check(TokenType type)
        => this.Current.Type == type;

    private bool Match(TokenType type)
    {
        if (this.Check(type))
        {
            this.Advance();
            return true;
        }

        return false;
    }

    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (this.Check(type))
            {
                this.Advance();
                return true;
            }
        }

        return false;
    }

    private Token Expect(TokenType type, string errorMessage)
    {
        if (!this.Check(type))
        {
            throw new ParserException(errorMessage, this.Current.Location);
        }

        var token = this.Current;
        this.Advance();
        return token;
    }

    private void Advance()
    {
        if (!this.IsAtEnd)
        {
            this.position++;
        }
    }
}
