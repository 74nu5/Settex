namespace Settex.Compilation;

/// <summary>
///     Result of a Settex compilation.
/// </summary>
public sealed class CompilationResult
{
    public CompilationResult(bool success, IReadOnlyList<Diagnostic> diagnostics)
    {
        this.Success = success;
        this.Diagnostics = diagnostics;
    }

    /// <summary>
    ///     Whether the compilation succeeded (no errors).
    /// </summary>
    public bool Success { get; }

    /// <summary>
    ///     All diagnostics (errors, warnings, info).
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    ///     Gets only error diagnostics.
    /// </summary>
    public IEnumerable<Diagnostic> Errors =>
        this.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    ///     Gets only warning diagnostics.
    /// </summary>
    public IEnumerable<Diagnostic> Warnings =>
        this.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);
}
