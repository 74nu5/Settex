namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Environment block: env "Name" { settings { ... } }
/// </summary>
public sealed record EnvBlockNode(
    string EnvironmentName,
    SettingsBlockNode SettingsBlock,
    SourceLocation Location) : ITopLevelStatement;
