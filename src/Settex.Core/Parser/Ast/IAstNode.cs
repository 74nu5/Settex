namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Base interface for all AST nodes.
///     All nodes must have a source location for diagnostics.
/// </summary>
public interface IAstNode
{
    SourceLocation Location { get; }
}
