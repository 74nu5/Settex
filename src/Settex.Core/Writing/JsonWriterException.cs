namespace Settex.Core.Writing;

using Settex.Core.Diagnostics;

/// <summary>
///     Exception thrown when writing JSON files fails.
/// </summary>
public sealed class JsonWriterException : Exception
{
    public JsonWriterException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public JsonWriterException(string message, SourceLocation? location, Exception? innerException = null)
        : base(message, innerException)
    {
        this.Location = location;
    }

    public SourceLocation? Location { get; }
}
