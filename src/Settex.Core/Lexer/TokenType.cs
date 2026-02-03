namespace Settex.Core.Lexer;

/// <summary>
///     Represents the type of a token in Settex source code.
/// </summary>
public enum TokenType
{
    // Keywords
    Settings,
    Env,
    True,
    False,
    Null,
    Include,

    // Symbols
    LeftBrace, // {
    RightBrace, // }
    LeftBracket, // [
    RightBracket, // ]
    Equals, // =
    Dot, // .
    Comma, // ,
    Semicolon, // ;

    // Literals
    String,
    Integer,
    Float,

    // Other
    Identifier,
    Newline, // Significant only inside arrays
    Comment, // Not emitted, but can be tracked for tooling
    Eof,
}
