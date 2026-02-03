namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Array: [ items ]
///     Items can be literals or tagged objects.
/// </summary>
public sealed record ArrayNode(
    List<IValue> Items,
    SourceLocation Location) : IValue;
