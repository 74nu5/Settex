namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents an element in an array.
///     Can be a value expression or a for loop.
/// </summary>
public interface IArrayElement;

/// <summary>
///     Array: [ items ]
///     Items can be literals, tagged objects, variable references, or for loops.
/// </summary>
public sealed record ArrayNode(
    List<IArrayElement> Elements,
    SourceLocation Location) : IValue, IExpression;
