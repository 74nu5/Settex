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
    Let,
    And,
    Or,
    Not,
    If,
    For,
    In,

    // Symbols
    LeftBrace, // {
    RightBrace, // }
    LeftBracket, // [
    RightBracket, // ]
    Equals, // =
    ColonEquals, // :=
    Dot, // .
    Comma, // ,
    Semicolon, // ;
    
    // Arithmetic operators
    Plus, // +
    Minus, // -
    Star, // *
    Slash, // /
    
    // Comparison operators
    EqualEqual, // ==
    NotEqual, // !=
    Less, // <
    LessEqual, // <=
    Greater, // >
    GreaterEqual, // >=
    
    // Null coalescing
    QuestionQuestion, // ??

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
