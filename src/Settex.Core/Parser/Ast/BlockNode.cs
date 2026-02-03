namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Block content: { stmt* }
///     Contains a list of statements (assignments or nested blocks).
/// </summary>
public sealed record BlockNode(
    List<IStatement> Statements,
    SourceLocation Location) : IAstNode;
