namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Assignment statement: path = value
///     Example: Server.Port = 8080
/// </summary>
public sealed record AssignmentNode(
    PathNode Path,
    IExpression Value,
    SourceLocation Location) : IStatement;
