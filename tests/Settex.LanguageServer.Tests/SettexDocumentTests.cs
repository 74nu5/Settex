using OmniSharp.Extensions.LanguageServer.Protocol;
using Settex.Core.Diagnostics;
using Settex.Core.Parser.Ast;

namespace Settex.LanguageServer.Tests;

public sealed class SettexDocumentTests
{
    [Test]
    public async Task IncludedSymbol_ResolvesToTheIncludedFile_NotTheCurrentDocumentAsync()
    {
        // A variable declared in an included file must, via go-to-definition,
        // point at the included file — not at the current document with the
        // included file's line numbers.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var commonPath = Path.Combine(tempDir, "common.settex");
            var mainPath = Path.Combine(tempDir, "main.settex");

            await File.WriteAllTextAsync(commonPath, "let shared = 5");
            await File.WriteAllTextAsync(
                mainPath,
                "include \"./common.settex\"\nsettings {\n    Port = shared\n}");

            var mainUri = DocumentUri.FromFileSystemPath(mainPath);
            var document = new SettexDocument(mainUri.ToString(), await File.ReadAllTextAsync(mainPath));

            // The included `let shared` is inlined as a top-level statement.
            var sharedLet = document.Ast!.Statements
                .OfType<LetNode>()
                .Single(l => l.Name == "shared");

            // Its SourceLocation must carry the included file's path.
            await Assert.That(sharedLet.Location.FilePath).IsNotNull();
            await Assert.That(Path.GetFileName(sharedLet.Location.FilePath!)).IsEqualTo("common.settex");

            // ToLspLocation must therefore point at common.settex, not main.settex.
            var location = SettexDocument.ToLspLocation(sharedLet.Location, mainUri);
            var resolvedPath = location.Uri.GetFileSystemPath();

            await Assert.That(Path.GetFileName(resolvedPath)).IsEqualTo("common.settex");
            await Assert.That(SamePath(resolvedPath, commonPath)).IsTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ToLspLocation_WithoutFilePath_FallsBackToCurrentUriAsync()
    {
        // An unsaved document produces locations without a FilePath; those must
        // resolve to the current document's URI.
        var currentUri = DocumentUri.Parse("untitled:Untitled-1");
        var location = new SourceLocation { Line = 1, Column = 1, Length = 3 };

        var result = SettexDocument.ToLspLocation(location, currentUri);

        await Assert.That(result.Uri).IsEqualTo(currentUri);
    }

    private static bool SamePath(string a, string b)
        => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
