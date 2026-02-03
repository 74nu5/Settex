namespace Settex.Core.Merging;

using Settex.Core.Diagnostics;

/// <summary>
///     Exception thrown when merging JSON objects fails.
/// </summary>
public sealed class MergerException(string message, SourceLocation? location = null) : Exception(message)
{
    public SourceLocation? Location { get; } = location;
}
