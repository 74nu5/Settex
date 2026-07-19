using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// A for-loop iterator is registered as a synthetic <c>let</c> whose "value" is the
/// collection being walked. Binding that in the hover's evaluation scope pointed the
/// iterator's name at the whole array, so any other variable in the loop body that
/// referenced it was evaluated against an array and rendered as a confident — and
/// wrong — value. An iterator has no single value; neither does a variable derived
/// from it, and saying nothing is the honest answer.
/// </summary>
public sealed class IteratorScopeTests
{
    private const string Source = """
        let ports = [1, 2]
        settings {
            Items = [ for p in ports {
                let doubled = p
                item { Value = doubled }
            } ]
        }
        """;

    [Test]
    public async Task Hover_OnAVariableDerivedFromTheIterator_DoesNotReportTheCollectionAsync()
    {
        var handler = CreateHandler(Source, out var uri);

        // Cursor on "doubled" where it is declared (0-based line 3).
        var hover = await handler.Handle(Request(uri, 3, 12), CancellationToken.None);

        if (hover is null)
        {
            // No hover at all is an acceptable answer here — what must not happen is a
            // confident wrong one.
            return;
        }

        var content = hover.Contents.MarkupContent!.Value;

        await Assert.That(content).DoesNotContain("[1, 2]");
        await Assert.That(content).DoesNotContain("[1,2]");
    }

    [Test]
    public async Task Hover_OnTheIteratorItself_StillAnswersAsync()
    {
        var handler = CreateHandler(Source, out var uri);

        // Cursor on the "p" of "for p in ports" (0-based line 2, column 18).
        var hover = await handler.Handle(Request(uri, 2, 18), CancellationToken.None);

        await Assert.That(hover).IsNotNull();
    }

    private static SettexHoverHandler CreateHandler(string source, out string uri)
    {
        var workspace = new SettexWorkspace();
        uri = "untitled:iterator-scope";
        workspace.DidOpen(uri, source);

        return new SettexHoverHandler(workspace, NullLogger<SettexHoverHandler>.Instance);
    }

    private static HoverParams Request(string uri, int line, int character) => new()
    {
        TextDocument = new TextDocumentIdentifier { Uri = uri },
        Position = new Position(line, character),
    };
}
