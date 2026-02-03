namespace Settex.Core.Resolution;

using Settex.Core.Diagnostics;

/// <summary>
///     Exception thrown when an include operation fails.
/// </summary>
public class IncludeException : Exception
{
    public SourceLocation? Location { get; }

    public IncludeException(string message, SourceLocation? location, Exception? innerException = null)
        : base(message, innerException)
    {
        this.Location = location;
    }
}
