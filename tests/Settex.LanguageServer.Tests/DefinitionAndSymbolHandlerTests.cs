using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// Go-to-definition and the document outline, neither of which had a direct test.
/// </summary>
public sealed class DefinitionAndSymbolHandlerTests
{
    [Test]
    public async Task Definition_OnAVariableUse_PointsAtItsDeclarationAsync()
    {
        const string source = """
            let basePort = 5000
            settings {
                Port = basePort
            }
            """;

        // Cursor on "basePort" where it is used (0-based line 2, column 11).
        var locations = await DefineAsync(source, 2, 11);

        await Assert.That(locations).IsNotNull();

        var location = locations!.Single();

        // The declaration is on line 0.
        await Assert.That(location.Location!.Range.Start.Line).IsEqualTo(0);
    }

    [Test]
    public async Task Definition_OnSomethingThatIsNotAVariable_ReturnsNothingAsync()
    {
        var locations = await DefineAsync("settings {\n    Port = 8080\n}", 1, 12);

        await Assert.That(locations is null || !locations.Any()).IsTrue();
    }

    [Test]
    public async Task Definition_OnADocumentTheServerDoesNotKnow_ReturnsNothingAsync()
    {
        var handler = new SettexDefinitionHandler(new SettexWorkspace(), NullLogger<SettexDefinitionHandler>.Instance);

        var result = await handler.Handle(DefinitionRequest("untitled:never-opened", 0, 0), CancellationToken.None);

        await Assert.That(result is null || !result.Any()).IsTrue();
    }

    [Test]
    public async Task Symbols_ListTheSettingsBlockEnvironmentsAndVariablesAsync()
    {
        const string source = """
            let basePort = 5000
            settings {
                Port = basePort
            }
            env "Production" {
                settings {
                    Port = 443
                }
            }
            """;

        var symbols = await SymbolsAsync(source);

        await Assert.That(symbols.Any(s => s.DocumentSymbol!.Name == "basePort")).IsTrue();
        await Assert.That(symbols.Any(s => s.DocumentSymbol!.Name == "settings")).IsTrue();
        await Assert.That(symbols.Any(s => s.DocumentSymbol!.Name == "env Production")).IsTrue();
    }

    [Test]
    public async Task Symbols_ExcludeThoseComingFromAnIncludedFileAsync()
    {
        // The outline describes the file you are looking at. Included statements are
        // merged into the same AST, so without a file check they showed up here too,
        // at line numbers belonging to another file.
        using var temp = new TempDir();

        temp.Write("lib.settex", "let fromInclude = 1\nsettings {\n    A = 1\n}");
        var mainPath = temp.Write("main.settex", "include \"./lib.settex\"\nlet fromMain = 2\nsettings {\n    B = 2\n}");

        var uri = DocumentUri.FromFileSystemPath(mainPath);
        var workspace = new SettexWorkspace();
        workspace.DidOpen(uri.ToString(), File.ReadAllText(mainPath));

        var handler = new SettexDocumentSymbolHandler(workspace, NullLogger<SettexDocumentSymbolHandler>.Instance);
        var result = await handler.Handle(
            new DocumentSymbolParams { TextDocument = new TextDocumentIdentifier { Uri = uri } },
            CancellationToken.None);

        var symbols = result!.ToList();

        await Assert.That(symbols.Any(s => s.DocumentSymbol!.Name == "fromMain")).IsTrue();
        await Assert.That(symbols.Any(s => s.DocumentSymbol!.Name == "fromInclude")).IsFalse();
    }

    [Test]
    public async Task Symbols_OnADocumentTheServerDoesNotKnow_ReturnNothingAsync()
    {
        var handler = new SettexDocumentSymbolHandler(new SettexWorkspace(), NullLogger<SettexDocumentSymbolHandler>.Instance);

        var result = await handler.Handle(
            new DocumentSymbolParams { TextDocument = new TextDocumentIdentifier { Uri = "untitled:never-opened" } },
            CancellationToken.None);

        await Assert.That(result is null || !result.Any()).IsTrue();
    }

    private static async Task<LocationOrLocationLinks?> DefineAsync(string source, int line, int character)
    {
        var workspace = new SettexWorkspace();
        const string uri = "untitled:definition-test";
        workspace.DidOpen(uri, source);

        var handler = new SettexDefinitionHandler(workspace, NullLogger<SettexDefinitionHandler>.Instance);

        return await handler.Handle(DefinitionRequest(uri, line, character), CancellationToken.None);
    }

    private static async Task<IReadOnlyList<SymbolInformationOrDocumentSymbol>> SymbolsAsync(string source)
    {
        var workspace = new SettexWorkspace();
        const string uri = "untitled:symbol-test";
        workspace.DidOpen(uri, source);

        var handler = new SettexDocumentSymbolHandler(workspace, NullLogger<SettexDocumentSymbolHandler>.Instance);
        var result = await handler.Handle(
            new DocumentSymbolParams { TextDocument = new TextDocumentIdentifier { Uri = uri } },
            CancellationToken.None);

        return result!.ToList();
    }

    private static DefinitionParams DefinitionRequest(string uri, int line, int character) => new()
    {
        TextDocument = new TextDocumentIdentifier { Uri = uri },
        Position = new Position(line, character),
    };

    private sealed class TempDir : IDisposable
    {
        private readonly string path;

        public TempDir()
        {
            this.path = Path.Combine(Path.GetTempPath(), $"settex-sym-{Guid.NewGuid():N}");
            Directory.CreateDirectory(this.path);
        }

        public string Write(string name, string content)
        {
            var full = Path.Combine(this.path, name);
            File.WriteAllText(full, content);
            return full;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.path, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory must never fail a test run.
            }
        }
    }
}
