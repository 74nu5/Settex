namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents a string with interpolated expressions: "text ${expr} text".
/// </summary>
public sealed record InterpolatedStringNode(
    List<StringSegment> Segments,
    SourceLocation Location
) : IExpression;

/// <summary>
///     Base type for string segments (literal or expression).
/// </summary>
public abstract record StringSegment;

/// <summary>
///     Literal text segment in an interpolated string.
/// </summary>
public sealed record LiteralSegment(string Text) : StringSegment;

/// <summary>
///     Expression segment in an interpolated string: ${expr}.
/// </summary>
public sealed record ExpressionSegment(IExpression Expression) : StringSegment;
