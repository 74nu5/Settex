namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Array: [ items ]
///     Items can be literals, tagged objects, or variable references.
/// </summary>
public sealed record ArrayNode(
    List<IExpression> Items,
    SourceLocation Location) : IValue;
