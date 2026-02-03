namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents a unary operation expression: op operand.
///     Examples: not condition, -5
/// </summary>
public sealed record UnaryOpNode(
    string Operator,
    IExpression Operand,
    SourceLocation Location
) : IExpression;
