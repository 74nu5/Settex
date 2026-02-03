namespace Settex.Core.Parser;

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

        this.Expect(TokenType.Eof, "Expected end of file");

        return new(statements, startLocation);
    }

    /// <summary>
    ///     Alias for Parse() - for backward compatibility.
    /// </summary>
    public FileNode ParseFile() => this.Parse();

    private Token Peek(int offset = 1) => this.position + offset < this.tokens.Count ? this.tokens[this.position + offset] : this.tokens[^1];

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

        var value = this.ParseExpression();

        return new(nameToken.Text, value, letToken.Location);
    }

    /// <summary>
    ///     Parses an expression (for now, just values or variable references - will be extended in Phase 3)
    /// </summary>
    private IExpression ParseExpression()
    {
        // For Phase 2, expressions are just values or variable references
        // Phase 3 will add binary operators, unary operators, etc.
        
        // Check for tagged object: identifier followed by '{'
        if (this.Check(TokenType.Identifier) && this.Peek().Type == TokenType.LeftBrace)
        {
            return this.ParseTaggedObjectValue();
        }

        // Check for variable reference: identifier NOT followed by '{'
        if (this.Check(TokenType.Identifier))
        {
            var nameToken = this.Current;
            this.Advance();
            return new VariableRefNode(nameToken.Text, nameToken.Location);
        }

        // Otherwise, parse as value (literal or array)
        var value = this.ParseValue();
        return value;
    }

    /// <summary>
    ///     Parses a settings block: "settings" block
    /// </summary>
    private SettingsBlockNode ParseSettingsBlock()
    {
        var settingsToken = this.Expect(TokenType.Settings, "Expected 'settings'");
        var block = this.ParseBlock();

        return new(block, settingsToken.Location);
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

        this.Expect(TokenType.RightBrace, "Expected '}' to close env block");

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

        return new(envName, settingsBlock, envToken.Location);
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

        this.Expect(TokenType.RightBrace, "Expected '}'");

        return new(statements, startToken.Location);
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
        // If we see: Ident "." → assignment (path)
        // If we see: Ident "{" → nested block
        if (this.Check(TokenType.Identifier))
        {
            if (this.Peek().Type == TokenType.Equals || this.Peek().Type == TokenType.Dot)
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
    ///     Parses an assignment statement: path "=" value/expression
    /// </summary>
    private AssignmentNode ParseAssignmentStatement()
    {
        var path = this.ParsePath();
        this.Expect(TokenType.Equals, "Expected '=' after path");
        var value = this.ParseExpression(); // Changed from ParseValue() to support variable refs

        return new(path, value, path.Location);
    }

    /// <summary>
    ///     Parses a nested block statement: ident block
    /// </summary>
    private NestedBlockNode ParseNestedBlockStatement()
    {
        var identToken = this.Expect(TokenType.Identifier, "Expected identifier");
        var block = this.ParseBlock();

        return new(identToken.Text, block, identToken.Location);
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
    private IValue ParseValue()
    {
        // Literal values
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
    /// </summary>
    private LiteralNode ParseLiteral()
    {
        var token = this.Current;
        this.Advance();

        return new(token.Value, token.Location);
    }

    /// <summary>
    ///     Parses an array: "[" arrayItems? "]"
    /// </summary>
    private ArrayNode ParseArray()
    {
        var startToken = this.Expect(TokenType.LeftBracket, "Expected '['");
        var items = new List<IExpression>();

        // Skip leading newlines
        while (this.Match(TokenType.Newline))
        {
        }

        // Empty array
        if (this.Check(TokenType.RightBracket))
        {
            this.Advance();
            return new(items, startToken.Location);
        }

        // Parse array items with comma or newline separators
        items.Add(this.ParseArrayItem());

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

                items.Add(this.ParseArrayItem());
            }
            else
            {
                throw new ParserException(
                    "Expected ',' or newline between array items",
                    this.Current.Location
                );
            }
        }

        this.Expect(TokenType.RightBracket, "Expected ']' to close array");

        return new(items, startToken.Location);
    }

    /// <summary>
    ///     Parses an array item: expression (literal, variable ref, or tagged object)
    /// </summary>
    private IExpression ParseArrayItem()
    {
        // Variable reference
        if (this.Check(TokenType.Identifier) && this.Peek().Type != TokenType.LeftBrace)
        {
            var nameToken = this.Current;
            this.Advance();
            return new VariableRefNode(nameToken.Text, nameToken.Location);
        }

        // Tagged object
        if (this.Check(TokenType.Identifier) && this.Peek().Type == TokenType.LeftBrace)
        {
            return this.ParseTaggedObjectValue();
        }

        // Literal
        if (this.Check(TokenType.String) || this.Check(TokenType.Integer) ||
            this.Check(TokenType.Float) || this.Check(TokenType.True) ||
            this.Check(TokenType.False) || this.Check(TokenType.Null))
        {
            return this.ParseLiteral();
        }

        throw new ParserException(
            $"Expected array item (literal, variable, or tagged object), but got '{this.Current.Text}'",
            this.Current.Location
        );
    }

    /// <summary>
    ///     Parses a tagged object value: ident block
    /// </summary>
    private TaggedObjectNode ParseTaggedObjectValue()
    {
        var identToken = this.Expect(TokenType.Identifier, "Expected identifier");
        var block = this.ParseBlock();

        return new(identToken.Text, block, identToken.Location);
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
