using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// Two lookups that answered from the wrong place: references never entered a
/// <c>for</c> loop, and hover picked a path segment by name rather than by position.
/// </summary>
public sealed class LoopReferenceAndSegmentTests
{
    [Test]
    public async Task References_ToAVariableUsedOnlyInsideALoop_AreFoundAsync()
    {
        // ForNode is an IArrayElement but not an IExpression, and the collector filtered
        // on IExpression — so neither the loop body nor the collection it walks was ever
        // visited, and this returned nothing at all.
        const string source = """
            let svcs = [1, 2]
            settings {
                Items = [ for s in svcs { item { Value = s } } ]
            }
            """;

        var (handler, uri) = CreateReferences(source);

        // Cursor on the declaration of `svcs` (0-based line 0, column 4).
        var locations = await handler.Handle(Request(uri, 0, 4), CancellationToken.None);

        await Assert.That(locations).IsNotNull();

        // The declaration plus its use inside the loop header.
        await Assert.That(locations!.Count()).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task References_ToAVariableUsedInALoopBody_AreFoundAsync()
    {
        const string source = """
            let prefix = "p"
            settings {
                Items = [ for s in [1] { item { Name = prefix } } ]
                Other = prefix
            }
            """;

        var (handler, uri) = CreateReferences(source);

        var locations = await handler.Handle(Request(uri, 0, 4), CancellationToken.None);

        await Assert.That(locations).IsNotNull();

        // Declaration, the use in the loop body, and the one outside it.
        await Assert.That(locations!.Count()).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task Hover_OnARepeatedPathSegment_UsesTheOneUnderTheCursorAsync()
    {
        // The path here is A.B.A. Picking the segment by IndexOf always returned 0, so
        // hovering the innermost assignment showed the overlay for the outer object A
        // instead of the value of A.B.A.
        const string source = "settings {\n    A {\n        B {\n            A = 1\n        }\n    }\n}";
        var handler = CreateHover(source, out var uri);

        // Cursor on the inner "A" (0-based line 3, column 12).
        var hover = await handler.Handle(HoverRequest(uri, 3, 12), CancellationToken.None);

        await Assert.That(hover).IsNotNull();

        var content = hover!.Contents.MarkupContent!.Value;

        await Assert.That(content).Contains("A.B.A");
    }

    [Test]
    public async Task Hover_OnAnObjectSegmentOfADottedPath_StillShowsTheObjectAsync()
    {
        // The guard: deriving the index from the column must not break the ordinary
        // case the old IndexOf handled correctly.
        const string source = "settings {\n    Server.Port = 8080\n}";
        var handler = CreateHover(source, out var uri);

        // Cursor on "Server" (0-based line 1, column 4).
        var hover = await handler.Handle(HoverRequest(uri, 1, 4), CancellationToken.None);

        await Assert.That(hover).IsNotNull();
    }

    private static (SettexReferencesHandler Handler, string Uri) CreateReferences(string source)
    {
        var workspace = new SettexWorkspace();
        var uri = "untitled:loop-refs";
        workspace.DidOpen(uri, source);

        return (new SettexReferencesHandler(workspace, NullLogger<SettexReferencesHandler>.Instance), uri);
    }

    private static SettexHoverHandler CreateHover(string source, out string uri)
    {
        var workspace = new SettexWorkspace();
        uri = "untitled:segment-hover";
        workspace.DidOpen(uri, source);

        return new SettexHoverHandler(workspace, NullLogger<SettexHoverHandler>.Instance);
    }

    private static ReferenceParams Request(string uri, int line, int character) => new()
    {
        TextDocument = new TextDocumentIdentifier { Uri = uri },
        Position = new Position(line, character),
        Context = new ReferenceContext { IncludeDeclaration = true },
    };

    private static HoverParams HoverRequest(string uri, int line, int character) => new()
    {
        TextDocument = new TextDocumentIdentifier { Uri = uri },
        Position = new Position(line, character),
    };
}
