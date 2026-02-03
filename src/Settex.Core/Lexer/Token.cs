namespace Settex.Core.Lexer;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents a token in Settex source code.
/// </summary>
public sealed record Token
{
    /// <summary>
    ///     The type of the token.
    /// </summary>
    public TokenType Type { get; init; }

    /// <summary>
    ///     The text content of the token.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    ///     The location of the token in source code.
    /// </summary>
    public SourceLocation Location { get; init; } = null!;

    /// <summary>
    ///     The literal value for String, Integer, and Float tokens.
    ///     For strings, this is the unescaped value.
    ///     For integers, this is the parsed long value.
    ///     For floats, this is the parsed double value.
    /// </summary>
    public object? Value { get; init; }

    public override string ToString()
    {
        if (this.Value != null && this.Type is TokenType.String or TokenType.Integer or TokenType.Float)
        {
            return $"{this.Type}({this.Value}) at {this.Location}";
        }

        return $"{this.Type}('{this.Text}') at {this.Location}";
    }
}
