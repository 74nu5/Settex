namespace Settex.Core.Evaluation;

using Settex.Core.Diagnostics;

/// <summary>
///     Exception thrown during evaluation of the AST.
/// </summary>
public sealed class EvaluatorException(string message, SourceLocation? location) : Exception(message)
{
    public SourceLocation? Location { get; } = location;

    public override string ToString()
        => this.Location is not null ? $"{this.Location}: {this.Message}" : this.Message;
}
