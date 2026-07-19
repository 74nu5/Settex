using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// The two notification handlers — document synchronisation and watched files — publish
/// diagnostics rather than returning them, so what they do is only visible through the
/// facade they publish to. <see cref="RecordingLanguageServer" /> stands in for it.
/// </summary>
public sealed class NotificationHandlerTests
{
    [Test]
    public async Task DidOpen_PublishesTheDocumentsDiagnosticsAsync()
    {
        var (handler, server) = CreateSync(out var workspace);

        // Drift the compiler warns about, so there is something to publish.
        const string source = """
            settings { App = "X" }
            env "Dev" { settings { OnlyDev = true } }
            env "Prod" { settings { App = "Z" } }
            """;

        await handler.Handle(OpenParams("untitled:sync-open", source), CancellationToken.None);

        await Assert.That(server.Published.Count).IsEqualTo(1);
        await Assert.That(server.Published[0].Uri).Contains("sync-open");
        await Assert.That(server.Published[0].Count).IsGreaterThan(0);
    }

    [Test]
    public async Task DidChange_RepublishesForTheChangedDocumentAsync()
    {
        var (handler, server) = CreateSync(out var workspace);
        const string uri = "untitled:sync-change";

        await handler.Handle(OpenParams(uri, "settings { A = 1 }"), CancellationToken.None);

        // A now-undefined variable: the change must produce a diagnostic where the open
        // produced none.
        await handler.Handle(ChangeParams(uri, "settings { A = missing }"), CancellationToken.None);

        await Assert.That(server.Published.Count).IsEqualTo(2);
        await Assert.That(server.Published[0].Count).IsEqualTo(0);
        await Assert.That(server.Published[1].Count).IsGreaterThan(0);
    }

    [Test]
    public async Task DidClose_ClearsTheClosedDocumentsDiagnosticsFirstAsync()
    {
        // The clear used to come after republishing the dependents, inside the same
        // guard — so a failure there left the closed file underlined with no way to
        // clear it. Order is the behaviour under test.
        var (handler, server) = CreateSync(out var workspace);
        const string uri = "untitled:sync-close";

        await handler.Handle(OpenParams(uri, "settings { A = missing }"), CancellationToken.None);
        await handler.Handle(CloseParams(uri), CancellationToken.None);

        var last = server.Published[^1];

        await Assert.That(last.Uri).Contains("sync-close");
        await Assert.That(last.Count).IsEqualTo(0);
    }

    [Test]
    public async Task WatchedFileChange_RepublishesTheDocumentsThatIncludeItAsync()
    {
        using var temp = new TempDir();

        var libPath = temp.Write("lib.settex", "let shared = 1");
        var mainPath = temp.Write("main.settex", "include \"./lib.settex\"\nsettings {\n    A = shared\n}");

        var workspace = new SettexWorkspace();
        var mainUri = DocumentUri.FromFileSystemPath(mainPath);
        workspace.DidOpen(mainUri.ToString(), File.ReadAllText(mainPath));

        var server = new RecordingLanguageServer();
        var handler = new SettexWatchedFilesHandler(workspace, server, NullLogger<SettexWatchedFilesHandler>.Instance);

        // The include is rewritten on disk so it no longer defines the variable.
        File.WriteAllText(libPath, "let somethingElse = 1");

        await handler.Handle(WatchedChange(libPath), CancellationToken.None);

        await Assert.That(server.Published.Count).IsEqualTo(1);
        await Assert.That(server.Published[0].Uri).Contains("main.settex");
        await Assert.That(server.Published[0].Count).IsGreaterThan(0);
    }

    [Test]
    public async Task WatchedFileChange_ForAFileNobodyIncludes_PublishesNothingAsync()
    {
        using var temp = new TempDir();

        var strayPath = temp.Write("stray.settex", "settings { A = 1 }");

        var server = new RecordingLanguageServer();
        var handler = new SettexWatchedFilesHandler(
            new SettexWorkspace(),
            server,
            NullLogger<SettexWatchedFilesHandler>.Instance);

        await handler.Handle(WatchedChange(strayPath), CancellationToken.None);

        await Assert.That(server.Published).IsEmpty();
    }

    private static (SettexTextDocumentSyncHandler Handler, RecordingLanguageServer Server) CreateSync(out SettexWorkspace workspace)
    {
        workspace = new SettexWorkspace();
        var server = new RecordingLanguageServer();

        return (
            new SettexTextDocumentSyncHandler(workspace, server, NullLogger<SettexTextDocumentSyncHandler>.Instance),
            server);
    }

    private static DidOpenTextDocumentParams OpenParams(string uri, string text) => new()
    {
        TextDocument = new TextDocumentItem { Uri = uri, LanguageId = "settex", Version = 1, Text = text },
    };

    private static DidChangeTextDocumentParams ChangeParams(string uri, string text) => new()
    {
        TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = uri, Version = 2 },
        ContentChanges = new Container<TextDocumentContentChangeEvent>(
            new TextDocumentContentChangeEvent { Text = text }),
    };

    private static DidCloseTextDocumentParams CloseParams(string uri) => new()
    {
        TextDocument = new TextDocumentIdentifier { Uri = uri },
    };

    private static DidChangeWatchedFilesParams WatchedChange(string path) => new()
    {
        Changes = new Container<FileEvent>(
            new FileEvent { Uri = DocumentUri.FromFileSystemPath(path), Type = FileChangeType.Changed }),
    };

    private sealed class TempDir : IDisposable
    {
        private readonly string path;

        public TempDir()
        {
            this.path = Path.Combine(Path.GetTempPath(), $"settex-notif-{Guid.NewGuid():N}");
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
