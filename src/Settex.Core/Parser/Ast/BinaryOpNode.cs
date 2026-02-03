namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents a binary operation expression: left op right.
///     Examples: 5 + 3, x == y, a and b, value ?? default
/// </summary>
public sealed record BinaryOpNode(
    IExpression Left,
    string Operator,
    IExpression Right,
    SourceLocation Location
) : IExpression;
