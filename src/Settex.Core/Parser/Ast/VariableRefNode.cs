namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents a variable reference in an expression.
///     Syntax: variableName
/// </summary>
public sealed record VariableRefNode(string Name, SourceLocation Location) : IExpression;
