using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// The completion handler is the largest file in the language server and the most
/// visible feature of both editor extensions, and it had no tests at all. These cover
/// what a user actually sees at each cursor position.
/// </summary>
public sealed class CompletionHandlerTests
{
    [Test]
    public async Task Completion_AfterADot_OffersThePropertiesOfThatObjectAsync()
    {
        const string source = """
            settings {
                Server {
                    Host = "localhost"
                    Port = 8080
                }
                Other = 1
            }
            env "Dev" {
                settings {
                    Server.Host = "dev"
                }
            }
            """;

        // Cursor immediately after "Server." on the overlay line (0-based line 9, col 15).
        var items = await CompleteAsync(source, 9, 15);

        await Assert.That(items.Any(i => i.Label == "Host")).IsTrue();
        await Assert.That(items.Any(i => i.Label == "Port")).IsTrue();

        // Only that object's properties — a sibling key is not one of them.
        await Assert.That(items.Any(i => i.Label == "Other")).IsFalse();
    }

    [Test]
    public async Task Completion_AtTopLevel_OffersTheLanguageKeywordsAsync()
    {
        var items = await CompleteAsync("settings {\n    A = 1\n}\n", 3, 0);

        await Assert.That(items.Any(i => i.Kind == CompletionItemKind.Keyword)).IsTrue();
    }

    [Test]
    public async Task Completion_OffersDeclaredVariablesAsync()
    {
        const string source = """
            let basePort = 5000
            settings {
                Port = basePort
            }
            """;

        // Cursor just after "Port = " (0-based line 2, column 11).
        var items = await CompleteAsync(source, 2, 11);

        await Assert.That(items.Any(i => i.Label == "basePort")).IsTrue();
    }

    /// <summary>
    /// A limitation worth recording rather than hiding: properties and variables both
    /// come from the evaluated AST, so while the file does not parse — which is most of
    /// the time completion is wanted — only the static keyword list is offered. The
    /// handler degrades instead of failing, but it does not help.
    /// </summary>
    [Test]
    public async Task Completion_WhileTheFileDoesNotParse_OffersKeywordsOnlyAsync()
    {
        const string source = "let basePort = 5000\nsettings {\n    Port = \n";

        var items = await CompleteAsync(source, 2, 11);

        await Assert.That(items.Any(i => i.Kind == CompletionItemKind.Keyword)).IsTrue();
        await Assert.That(items.Any(i => i.Label == "basePort")).IsFalse();
    }

    [Test]
    public async Task Completion_OffersDeclaredEnvironmentsAsync()
    {
        const string source = """
            settings { A = 1 }
            env "Staging" { settings { A = 2 } }
            env "Production" { settings { A = 3 } }
            """;

        var items = await CompleteAsync(source, 3, 0);

        await Assert.That(items.Any(i => i.Label == "Staging")).IsTrue();
        await Assert.That(items.Any(i => i.Label == "Production")).IsTrue();
    }

    [Test]
    public async Task Completion_OnADocumentTheServerDoesNotKnow_ReturnsNothingAsync()
    {
        // The handler must degrade rather than throw when asked about a URI that was
        // never opened — a race the client can genuinely produce.
        var handler = new SettexCompletionHandler(new SettexWorkspace(), NullLogger<SettexCompletionHandler>.Instance);

        var result = await handler.Handle(Request("untitled:never-opened", 0, 0), CancellationToken.None);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Completion_OnAnUnparsableDocument_StillAnswersAsync()
    {
        // Completion is most useful exactly while the file is half-written and does not
        // parse. It must not fault.
        var items = await CompleteAsync("settings {\n    A = \n", 1, 8);

        await Assert.That(items).IsNotNull();
    }

    private static async Task<IReadOnlyList<CompletionItem>> CompleteAsync(string source, int line, int character)
    {
        var workspace = new SettexWorkspace();
        const string uri = "untitled:completion-test";
        workspace.DidOpen(uri, source);

        var handler = new SettexCompletionHandler(workspace, NullLogger<SettexCompletionHandler>.Instance);
        var result = await handler.Handle(Request(uri, line, character), CancellationToken.None);

        return result.Items.ToList();
    }

    private static CompletionParams Request(string uri, int line, int character) => new()
    {
        TextDocument = new TextDocumentIdentifier { Uri = uri },
        Position = new Position(line, character),
    };
}
