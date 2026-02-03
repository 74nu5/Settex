namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Root node of a Settex file.
///     Contains all top-level statements (settings blocks and env blocks).
/// </summary>
public sealed record FileNode(
    List<ITopLevelStatement> Statements,
    SourceLocation Location) : IAstNode;
