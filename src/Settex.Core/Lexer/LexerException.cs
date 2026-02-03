namespace Settex.Core.Lexer;

using Settex.Core.Diagnostics;

/// <summary>
///     Exception thrown when the lexer encounters an error.
/// </summary>
public sealed class LexerException : Exception
{
    public LexerException(string message, SourceLocation location)
            : base($"{location}: {message}")
        => this.Location = location;

    public LexerException(string message, SourceLocation location, Exception innerException)
            : base($"{location}: {message}", innerException)
        => this.Location = location;

    public SourceLocation Location { get; }
}
