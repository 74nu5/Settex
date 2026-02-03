namespace Settex.Core.Parser.Ast;

/// <summary>
///     Interface for all value nodes.
///     Values can be literals, arrays, or tagged objects.
///     In V2, all values are also expressions.
/// </summary>
public interface IValue : IExpression
{
}
