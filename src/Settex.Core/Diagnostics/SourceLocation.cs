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
    ///     The length of the span in characters. Only meaningful for a single-line
    ///     span; multi-line nodes carry <see cref="EndLine" />/<see cref="EndColumn" />.
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    ///     The last line of the span (1-based), when the node spans several lines
    ///     (a block, an array, a file). Null for single-line spans such as tokens.
    /// </summary>
    public int? EndLine { get; init; }

    /// <summary>
    ///     The exclusive end column of the span (1-based), paired with
    ///     <see cref="EndLine" />. Null for single-line spans.
    /// </summary>
    public int? EndColumn { get; init; }

    /// <summary>
    ///     End line of the span, falling back to <see cref="Line" /> for single-line
    ///     spans. Use this rather than <see cref="EndLine" /> when you need a value.
    /// </summary>
    public int EffectiveEndLine => this.EndLine ?? this.Line;

    /// <summary>
    ///     Exclusive end column of the span, falling back to
    ///     <see cref="Column" /> + <see cref="Length" /> for single-line spans.
    /// </summary>
    public int EffectiveEndColumn => this.EndColumn ?? (this.Column + this.Length);

    public override string ToString()
    {
        var file = this.FilePath ?? "<source>";
        return $"{file}({this.Line},{this.Column})";
    }
}
