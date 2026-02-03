namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents a let statement (variable declaration).
///     Syntax: let name = expr
/// </summary>
public sealed record LetNode(string Name, IExpression Value, SourceLocation Location) : ITopLevelStatement, IStatement;
