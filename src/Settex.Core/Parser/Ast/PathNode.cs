namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Path: a.b.c - sequence of identifiers separated by dots.
///     Example: Logging.LogLevel.Default
/// </summary>
public sealed record PathNode(
    List<string> Segments,
    SourceLocation Location) : IAstNode;
