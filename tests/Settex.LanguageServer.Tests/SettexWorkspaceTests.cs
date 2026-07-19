using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// The workspace must resolve <c>include</c>s against open (possibly unsaved)
/// buffers rather than the on-disk copy, and re-analyse the documents that depend
/// on a file when that file changes, opens or closes.
/// </summary>
public sealed class SettexWorkspaceTests
{
    [Test]
    public async Task OpeningIncludedFile_UsesUnsavedBufferAndRefreshesDependentsAsync()
    {
        using var files = new TempFiles();
        var commonPath = files.Write("common.settex", "let shared = 1");
        var mainPath = files.Write("main.settex", "include \"./common.settex\"\nsettings { X = shared }");

        var workspace = new SettexWorkspace();
        var mainUri = ToUri(mainPath);
        workspace.DidOpen(mainUri, await File.ReadAllTextAsync(mainPath));

        var main = workspace.GetDocument(mainUri)!;
        await Assert.That(main.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)).IsFalse();

        // Open the included file with unsaved content that no longer defines 'shared'.
        var affected = workspace.DidOpen(ToUri(commonPath), "let other = 2");

        // The including document was re-analysed against the buffer, not the disk.
        await Assert.That(affected.Any(d => d.Uri == mainUri)).IsTrue();
        await Assert.That(main.Diagnostics.Any(d =>
            d.Severity == DiagnosticSeverity.Error && d.Message.Contains("shared"))).IsTrue();
    }

    [Test]
    public async Task ChangingIncludedFile_RefreshesDependentsAsync()
    {
        using var files = new TempFiles();
        var commonPath = files.Write("common.settex", "let shared = 1");
        var mainPath = files.Write("main.settex", "include \"./common.settex\"\nsettings { X = shared }");

        var workspace = new SettexWorkspace();
        var mainUri = ToUri(mainPath);
        var commonUri = ToUri(commonPath);

        workspace.DidOpen(mainUri, await File.ReadAllTextAsync(mainPath));
        workspace.DidOpen(commonUri, await File.ReadAllTextAsync(commonPath));

        var main = workspace.GetDocument(mainUri)!;
        await Assert.That(main.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)).IsFalse();

        // Typing in the included file must invalidate the including document.
        var affected = workspace.DidChange(commonUri, "let other = 2");

        await Assert.That(affected.Any(d => d.Uri == mainUri)).IsTrue();
        await Assert.That(main.Diagnostics.Any(d =>
            d.Severity == DiagnosticSeverity.Error && d.Message.Contains("shared"))).IsTrue();
    }

    [Test]
    public async Task ClosingIncludedFile_FallsBackToDiskAsync()
    {
        using var files = new TempFiles();
        var commonPath = files.Write("common.settex", "let shared = 1");
        var mainPath = files.Write("main.settex", "include \"./common.settex\"\nsettings { X = shared }");

        var workspace = new SettexWorkspace();
        var mainUri = ToUri(mainPath);
        var commonUri = ToUri(commonPath);

        workspace.DidOpen(mainUri, await File.ReadAllTextAsync(mainPath));
        workspace.DidOpen(commonUri, "let other = 2");

        var main = workspace.GetDocument(mainUri)!;
        await Assert.That(main.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)).IsTrue();

        // Closing the buffer restores the on-disk content, which still defines 'shared'.
        var affected = workspace.DidClose(commonUri);

        await Assert.That(affected.Any(d => d.Uri == mainUri)).IsTrue();
        await Assert.That(main.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)).IsFalse();
    }

    [Test]
    public async Task Document_TracksTheFilesItIncludesAsync()
    {
        using var files = new TempFiles();
        var commonPath = files.Write("common.settex", "let shared = 1");
        var mainPath = files.Write("main.settex", "include \"./common.settex\"\nsettings { X = shared }");

        var workspace = new SettexWorkspace();
        var mainUri = ToUri(mainPath);
        workspace.DidOpen(mainUri, await File.ReadAllTextAsync(mainPath));

        var main = workspace.GetDocument(mainUri)!;

        await Assert.That(main.Includes.Any(i => SettexDocument.SamePath(i, commonPath))).IsTrue();
    }

    [Test]
    public async Task UnrelatedDocument_IsNotRefreshedAsync()
    {
        using var files = new TempFiles();
        var commonPath = files.Write("common.settex", "let shared = 1");
        var otherPath = files.Write("other.settex", "settings { Y = 1 }");

        var workspace = new SettexWorkspace();
        workspace.DidOpen(ToUri(otherPath), await File.ReadAllTextAsync(otherPath));

        var affected = workspace.DidOpen(ToUri(commonPath), "let shared = 2");

        // Only the opened document itself; 'other' does not include it.
        await Assert.That(affected.Count).IsEqualTo(1);
    }

    private static string ToUri(string path) => DocumentUri.FromFileSystemPath(path).ToString();

    private sealed class TempFiles : IDisposable
    {
        private readonly string root;

        public TempFiles()
        {
            this.root = Path.Combine(Path.GetTempPath(), "settex-ws-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(this.root);
        }

        public string Write(string name, string content)
        {
            var path = Path.Combine(this.root, name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
