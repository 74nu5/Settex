namespace Settex.Compilation;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents a diagnostic message (error, warning, info).
/// </summary>
public sealed class Diagnostic
{
    public Diagnostic(
        DiagnosticSeverity severity,
        string message,
        SourceLocation? location = null,
        string? keyPath = null,
        string? environmentName = null)
    {
        this.Severity = severity;
        this.Message = message;
        this.Location = location;
        this.KeyPath = keyPath;
        this.EnvironmentName = environmentName;
    }

    public DiagnosticSeverity Severity { get; }
    public string Message { get; }
    public SourceLocation? Location { get; }

    /// <summary>
    ///     The dotted configuration key this diagnostic is about, when it has one.
    ///     <para>
    ///     The analyzers run on the evaluated model, which is plain JSON and has lost
    ///     every source position — so they cannot produce a <see cref="Location" />
    ///     themselves. Naming the key lets a host that still holds the AST anchor the
    ///     diagnostic on the assignment that introduced it, instead of stacking every
    ///     warning on the first character of the file.
    ///     </para>
    /// </summary>
    public string? KeyPath { get; }

    /// <summary>
    ///     The environment this diagnostic is about, when it is about one. Both
    ///     analyzers report an environment <em>overriding</em> or <em>missing</em>
    ///     something, so the assignment worth pointing at lives in that environment's
    ///     block — not in the base, which may well assign the same key.
    /// </summary>
    public string? EnvironmentName { get; }

    public override string ToString()
    {
        if (this.Location != null)
        {
            return $"{this.Location}: {this.Severity.ToString().ToLowerInvariant()}: {this.Message}";
        }

        return $"{this.Severity.ToString().ToLowerInvariant()}: {this.Message}";
    }
}

/// <summary>
///     Severity level for diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}
