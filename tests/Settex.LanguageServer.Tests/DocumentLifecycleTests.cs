using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// How a document is re-analysed when something other than its own buffer changes:
/// a file it includes was edited elsewhere, created after being missing, or changed
/// on disk entirely outside the editor.
/// </summary>
public sealed class DocumentLifecycleTests
{
    [Test]
    public async Task Refresh_RacingUpdate_NeverRewindsTheBufferAsync()
    {
        // Refresh re-reads the current text, so it is a read-modify-write, and it has a
        // different caller from Update: Update comes from this document's didChange,
        // Refresh from a change to a file it includes. Interleaved without
        // serialisation, Refresh re-parsed text captured before an Update and published
        // it, rewinding the server's copy of the buffer.
        var document = new SettexDocument("untitled:race", Text(0));

        const int iterations = 400;

        var updates = Task.Run(() =>
        {
            for (var i = 1; i <= iterations; i++)
            {
                document.Update(Text(i));
            }
        });

        var refreshes = Task.Run(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                document.Refresh();
            }
        });

        await Task.WhenAll(updates, refreshes);

        await Assert.That(document.Text).IsEqualTo(Text(iterations));

        static string Text(int value) => $"settings {{\n    M = {value}\n}}";
    }

    [Test]
    public async Task Includes_WhenTheTargetIsMissing_StillTrackTheMissingFileAsync()
    {
        // The file that was *not* found is the one whose creation must re-analyse this
        // document. Tracking only successfully resolved files meant the STX301 error
        // could never clear on its own.
        using var temp = new TempDirectory();

        var mainPath = temp.Write("main.settex", "include \"./later.settex\"\nsettings {\n    A = 1\n}");
        var laterPath = Path.Combine(temp.Path, "later.settex");

        var workspace = new SettexWorkspace();
        var opened = workspace.DidOpen(DocumentUri.FromFileSystemPath(mainPath).ToString(), File.ReadAllText(mainPath));
        var main = opened[0];

        await Assert.That(main.Diagnostics.Any(d => d.Code == "STX301")).IsTrue();
        await Assert.That(main.Includes.Any(i => SettexDocument.SamePath(i, laterPath))).IsTrue();
    }

    [Test]
    public async Task CreatingAMissingInclude_ClearsTheErrorWithoutTouchingTheDocumentAsync()
    {
        using var temp = new TempDirectory();

        var mainPath = temp.Write("main.settex", "include \"./later.settex\"\nsettings {\n    A = shared\n}");

        var workspace = new SettexWorkspace();
        var mainUri = DocumentUri.FromFileSystemPath(mainPath).ToString();
        var main = workspace.DidOpen(mainUri, File.ReadAllText(mainPath))[0];

        await Assert.That(main.Diagnostics).IsNotEmpty();

        // The missing file appears on disk. Nothing about main's own buffer changed.
        var laterPath = temp.Write("later.settex", "let shared = 5");

        var refreshed = workspace.RefreshDependentsOf(laterPath);

        await Assert.That(refreshed.Any(d => d.Uri == mainUri)).IsTrue();
        await Assert.That(main.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task OnDiskChangeToAnIncludedFile_RefreshesDependentsAsync()
    {
        // The case open-buffer resolution cannot cover: a git checkout or another tool
        // rewrites an included file that no editor buffer knows about.
        using var temp = new TempDirectory();

        var libPath = temp.Write("lib.settex", "let shared = 1");
        var mainPath = temp.Write("main.settex", "include \"./lib.settex\"\nsettings {\n    A = shared\n}");

        var workspace = new SettexWorkspace();
        var mainUri = DocumentUri.FromFileSystemPath(mainPath).ToString();
        var main = workspace.DidOpen(mainUri, File.ReadAllText(mainPath))[0];

        await Assert.That(main.Diagnostics).IsEmpty();

        // Rewrite the include so it no longer defines the variable main uses.
        File.WriteAllText(libPath, "let somethingElse = 1");

        var refreshed = workspace.RefreshDependentsOf(libPath);

        await Assert.That(refreshed.Any(d => d.Uri == mainUri)).IsTrue();
        await Assert.That(main.Diagnostics).IsNotEmpty();
    }

    [Test]
    public async Task OnDiskChangeToAnOpenFile_LeavesItsOwnBufferAloneAsync()
    {
        // A file open in the editor is authoritative: re-reading the disk copy would
        // discard unsaved edits. Only the documents that include it are refreshed.
        using var temp = new TempDirectory();

        var libPath = temp.Write("lib.settex", "let shared = 1\nsettings {\n    B = 2\n}");

        var workspace = new SettexWorkspace();
        var libUri = DocumentUri.FromFileSystemPath(libPath).ToString();

        // The open buffer has an unsaved edit the disk copy does not.
        const string unsaved = "let shared = 99\nsettings {\n    B = 2\n}";
        var lib = workspace.DidOpen(libUri, unsaved)[0];

        workspace.RefreshDependentsOf(libPath);

        await Assert.That(lib.Text).IsEqualTo(unsaved);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"settex-lifecycle-{Guid.NewGuid():N}");
            Directory.CreateDirectory(this.Path);
        }

        public string Path { get; }

        public string Write(string name, string content)
        {
            var full = System.IO.Path.Combine(this.Path, name);
            File.WriteAllText(full, content);
            return full;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.Path, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory must never fail a test run.
            }
        }
    }
}
