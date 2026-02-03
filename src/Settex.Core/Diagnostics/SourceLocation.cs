namespace Settex.Core.Diagnostics;

/// <summary>
///     Represents a location in source code.
/// </summary>
public sealed record SourceLocation
{
    /// <summary>
    ///     The file path of the source code. Null if source is from string.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    ///     The line number (1-based).
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    ///     The column number (1-based).
    /// </summary>
    public int Column { get; init; }

    /// <summary>
    ///     The length of the span in characters.
    /// </summary>
    public int Length { get; init; }

    public override string ToString()
    {
        var file = this.FilePath ?? "<source>";
        return $"{file}({this.Line},{this.Column})";
    }
}
