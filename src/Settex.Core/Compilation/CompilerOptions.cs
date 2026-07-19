namespace Settex.Compilation;

/// <summary>
///     Options controlling a Settex compilation.
/// </summary>
public sealed class CompilerOptions
{
    /// <summary>
    ///     When <c>true</c>, each <c>appsettings.{Env}.json</c> contains the full
    ///     merged configuration (base + overlay). When <c>false</c> (the default),
    ///     it contains only the environment's overrides, which .NET configuration
    ///     layers on top of <c>appsettings.json</c> at runtime — smaller, cleaner
    ///     diffs, and faithful to the native layering model.
    /// </summary>
    public bool MergeEnvironments { get; init; }

    /// <summary>
    ///     When <c>true</c> (the default), the compiler warns about keys defined for
    ///     some environments but missing from others (and from the base settings) —
    ///     the configuration drift Settex exists to catch.
    /// </summary>
    public bool CheckCoverage { get; init; } = true;

    /// <summary>
    ///     Whether to warn when an environment override would leak base array content
    ///     through .NET's index-based layering. Separate from <see cref="CheckCoverage" />:
    ///     the two report different hazards, and sharing one switch meant silencing the
    ///     drift check also silenced this one.
    /// </summary>
    public bool CheckArrayLayering { get; init; } = true;

    /// <summary>Default options: delta output, coverage check enabled.</summary>
    public static CompilerOptions Default { get; } = new();
}
