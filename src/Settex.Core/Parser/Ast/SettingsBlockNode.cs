namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Settings block: settings { ... }
/// </summary>
public sealed record SettingsBlockNode(
    BlockNode Block,
    SourceLocation Location) : ITopLevelStatement;
