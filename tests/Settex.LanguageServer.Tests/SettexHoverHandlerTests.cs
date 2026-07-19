using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// Hover must find an assignment through nested blocks and report it under its
/// full dotted path, and must not trigger merely because the cursor sits on the
/// same line as an assignment.
/// </summary>
public sealed class SettexHoverHandlerTests
{
    [Test]
    public async Task Hover_OnAssignmentInsideNestedBlock_ReturnsOverlayAsync()
    {
        // 1: settings {   2: Server {   3: Port = 8080   4: }   5: }
        const string source = "settings {\n    Server {\n        Port = 8080\n    }\n}";
        var (handler, uri) = CreateHandler(source);

        // Cursor on "Port" (line 3 -> 0-based 2, column 8).
        var hover = await handler.Handle(Request(uri, 2, 8), CancellationToken.None);

        await Assert.That(hover).IsNotNull();
    }

    [Test]
    public async Task Hover_OnTopLevelAssignment_StillReturnsOverlayAsync()
    {
        const string source = "settings {\n    Port = 8080\n}";
        var (handler, uri) = CreateHandler(source);

        var hover = await handler.Handle(Request(uri, 1, 4), CancellationToken.None);

        await Assert.That(hover).IsNotNull();
    }

    [Test]
    public async Task Hover_OnWordAfterAssignment_DoesNotReturnOverlayAsync()
    {
        // The check is column-aware now. A trailing comment repeating the key sits
        // outside the assignment's span, so hovering it must not surface the
        // assignment's overlay — previously any position on the line matched.
        const string source = "settings {\n    Port = 8080 // Port\n}";
        var (handler, uri) = CreateHandler(source);

        // Cursor on the "Port" inside the comment (0-based line 1, column 19).
        var hover = await handler.Handle(Request(uri, 1, 19), CancellationToken.None);

        await Assert.That(hover).IsNull();
    }

    private static (SettexHoverHandler Handler, string Uri) CreateHandler(string source)
    {
        var workspace = new SettexWorkspace();
        var uri = "untitled:hover-test";
        workspace.DidOpen(uri, source);

        return (new SettexHoverHandler(workspace, NullLogger<SettexHoverHandler>.Instance), uri);
    }

    private static HoverParams Request(string uri, int line, int character) => new()
    {
        TextDocument = new TextDocumentIdentifier(DocumentUri.Parse(uri)),
        Position = new Position(line, character),
    };
}
