namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Assignment statement: path = value [if condition]
///     Example: Server.Port = 8080
///     Example with condition: LogLevel = "Debug" if env == "Development"
/// </summary>
public sealed record AssignmentNode(
    PathNode Path,
    IExpression Value,
    IExpression? Condition,
    SourceLocation Location) : IStatement;
