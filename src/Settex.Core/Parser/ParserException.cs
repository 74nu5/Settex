namespace Settex.Core.Parser;

using Settex.Core.Diagnostics;

/// <summary>
///     Exception thrown during parsing.
/// </summary>
public sealed class ParserException(string message, SourceLocation location) : Exception(message)
{
    public SourceLocation Location { get; } = location;

    public override string ToString()
        => $"{this.Location}: {this.Message}";
}
