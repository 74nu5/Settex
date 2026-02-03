namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Nested block statement: Ident { ... }
///     Example: Logging { LogLevel { Default = "Debug" } }
///     This constructs a JSON object.
/// </summary>
public sealed record NestedBlockNode(
    string Name,
    BlockNode Block,
    SourceLocation Location) : IStatement;
