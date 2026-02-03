namespace Settex.Compilation;

using Settex.Core.Diagnostics;

/// <summary>
///     Represents a diagnostic message (error, warning, info).
/// </summary>
public sealed class Diagnostic
{
    public Diagnostic(DiagnosticSeverity severity, string message, SourceLocation? location = null)
    {
        this.Severity = severity;
        this.Message = message;
        this.Location = location;
    }

    public DiagnosticSeverity Severity { get; }
    public string Message { get; }
    public SourceLocation? Location { get; }

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
