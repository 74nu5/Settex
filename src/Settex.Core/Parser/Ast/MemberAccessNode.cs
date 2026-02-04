namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents member access on an object in an expression.
///     Syntax: object.memberName
///     Example: user.Name, server.Port
/// </summary>
public sealed record MemberAccessNode(
    IExpression Object,
    string MemberName,
    SourceLocation Location
) : IExpression;
