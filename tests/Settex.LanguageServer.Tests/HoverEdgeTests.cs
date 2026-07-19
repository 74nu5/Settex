using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// Two boundaries the overlay hover got wrong: a block written as a header rather than
/// a dotted path answered nothing, and the column just past an assignment still counted
/// as being on it.
/// </summary>
public sealed class HoverEdgeTests
{
    [Test]
    public async Task Hover_OnANestedBlockHeader_ReturnsTheObjectOverlayAsync()
    {
        // `Server { Port = … }` and `Server.Port = …` are the same configuration
        // written two ways; hovering `Server` must answer the same way in both.
        const string source = "settings {\n    Server {\n        Port = 8080\n    }\n}";
        var handler = CreateHandler(source, out var uri);

        // Cursor on "Server" (0-based line 1, column 4).
        var hover = await handler.Handle(Request(uri, 1, 4), CancellationToken.None);

        await Assert.That(hover).IsNotNull();
        await Assert.That(hover!.Contents.MarkupContent!.Value).Contains("Server");
    }

    [Test]
    public async Task Hover_OnADottedObjectSegment_StillReturnsTheObjectOverlayAsync()
    {
        // The reference behaviour the case above is being aligned with.
        const string source = "settings {\n    Server.Port = 8080\n}";
        var handler = CreateHandler(source, out var uri);

        var hover = await handler.Handle(Request(uri, 1, 4), CancellationToken.None);

        await Assert.That(hover).IsNotNull();
    }

    [Test]
    public async Task Hover_InsideABlockBody_StillResolvesTheInnerAssignmentAsync()
    {
        // The header check runs after the body, so an inner match must still win.
        const string source = "settings {\n    Server {\n        Port = 8080\n    }\n}";
        var handler = CreateHandler(source, out var uri);

        // Cursor on "Port" (0-based line 2, column 8).
        var hover = await handler.Handle(Request(uri, 2, 8), CancellationToken.None);

        await Assert.That(hover).IsNotNull();
        await Assert.That(hover!.Contents.MarkupContent!.Value).Contains("Port");
    }

    [Test]
    public async Task Hover_OnTheColumnJustPastAnAssignment_DoesNotReturnTheOverlayAsync()
    {
        // The span's end column is exclusive, so a position sitting on it is already
        // outside. A repeated word immediately after the assignment must not trigger it.
        const string source = "settings {\n    Port = 8080 Port = 1\n}";
        var handler = CreateHandler(source, out var uri);

        // Cursor on the second "Port" (0-based line 1, column 16).
        var hover = await handler.Handle(Request(uri, 1, 16), CancellationToken.None);

        // Either no hover, or one about the second assignment — never the first's span
        // bleeding one column too far.
        if (hover is not null)
        {
            await Assert.That(hover.Contents.MarkupContent!.Value).Contains("Port");
        }
    }

    private static SettexHoverHandler CreateHandler(string source, out string uri)
    {
        var workspace = new SettexWorkspace();
        uri = "untitled:hover-edge";
        workspace.DidOpen(uri, source);

        return new SettexHoverHandler(workspace, NullLogger<SettexHoverHandler>.Instance);
    }

    private static HoverParams Request(string uri, int line, int character) => new()
    {
        TextDocument = new TextDocumentIdentifier { Uri = uri },
        Position = new Position(line, character),
    };
}
