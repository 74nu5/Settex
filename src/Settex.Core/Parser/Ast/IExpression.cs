namespace Settex.Core.Parser.Ast;

/// <summary>
///     Interface for all expression nodes in V2.
///     Expressions can be literals, variables, binary operations, etc.
///     All expressions can also be used as array elements.
/// </summary>
public interface IExpression : IAstNode, IArrayElement
{
}
