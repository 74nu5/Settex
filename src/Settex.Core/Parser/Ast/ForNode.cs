namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     For loop in an array: for ident in expr { ... }
///     Example: for service in services { item { Name = service.name } }
/// </summary>
public sealed record ForNode(
    string IteratorName,
    IExpression Collection,
    BlockNode Body,
    SourceLocation Location) : IArrayElement;
