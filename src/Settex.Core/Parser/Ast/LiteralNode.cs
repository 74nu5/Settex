namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Literal value: string, number, bool, or null.
/// </summary>
public sealed record LiteralNode(
    object? Value,
    SourceLocation Location) : IValue, IExpression;
