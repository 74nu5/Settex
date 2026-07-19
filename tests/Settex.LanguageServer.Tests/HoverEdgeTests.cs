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

    /// <summary>
    /// This used to guard its only assertion behind `if (hover is not null)` and use a
    /// source where every candidate overlay contained the word it checked for — so it
    /// passed whether or not the span bled one column past the assignment, which is the
    /// regression it exists to forbid. The two keys are distinguishable now, and the
    /// assertion is unconditional.
    /// </summary>
    [Test]
    public async Task Hover_OnTheColumnJustPastAnAssignment_DoesNotReturnTheFirstOverlayAsync()
    {
        const string source = "settings {\n    Alpha = 1 Beta = 2\n}";
        var handler = CreateHandler(source, out var uri);

        // Cursor on "Beta", the first column past the `Alpha = 1` assignment
        // (0-based line 1, column 14).
        var hover = await handler.Handle(Request(uri, 1, 14), CancellationToken.None);

        await Assert.That(hover).IsNotNull();

        var content = hover!.Contents.MarkupContent!.Value;

        // Whatever it reports, it must be about Beta — never Alpha's overlay bleeding
        // one column too far.
        await Assert.That(content).Contains("Beta");
        await Assert.That(content).DoesNotContain("Alpha");
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
