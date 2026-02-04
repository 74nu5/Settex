namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents the type of assignment operation.
/// </summary>
public enum AssignmentOp
{
    /// <summary>
    ///     Normal assignment (=): Always sets the value.
    /// </summary>
    Set,
    
    /// <summary>
    ///     Set-if-missing (:=): Only sets if the key doesn't exist.
    /// </summary>
    SetIfMissing,
}

/// <summary>
///     Assignment statement: path = value [if condition]
///     Example: Server.Port = 8080
///     Example with condition: LogLevel = "Debug" if env == "Development"
///     Example with :=: Port := 8080 (sets only if not already defined)
/// </summary>
public sealed record AssignmentNode(
    PathNode Path,
    AssignmentOp Op,
    IExpression Value,
    IExpression? Condition,
    SourceLocation Location) : IStatement;
