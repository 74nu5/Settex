namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents an include statement in the AST.
///     Syntax: include "./path/to/file.settex"
/// </summary>
public sealed record IncludeNode(string Path, SourceLocation Location) : ITopLevelStatement;
