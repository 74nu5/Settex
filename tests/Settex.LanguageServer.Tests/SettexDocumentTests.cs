using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Settex.Core.Diagnostics;
using Settex.Core.Parser.Ast;

namespace Settex.LanguageServer.Tests;

public sealed class SettexDocumentTests
{
    [Test]
    public async Task Diagnostics_UndefinedVariable_ReportsErrorAsync()
    {
        // Semantic (evaluation) errors must now show up in the editor, not only at
        // the CLI/build.
        var doc = new SettexDocument("untitled:Untitled-1", "settings { Port = undefinedVar }");

        await Assert.That(doc.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error
            && d.Message.Contains("undefinedVar"))).IsTrue();
    }

    [Test]
    public async Task Diagnostics_TypeMismatch_ReportsErrorAsync()
    {
        var doc = new SettexDocument("untitled:Untitled-1", "settings { Foo = 1 }\nsettings { Foo { Bar = 2 } }");

        await Assert.That(doc.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error
            && d.Message.Contains("Foo"))).IsTrue();
    }

    [Test]
    public async Task Diagnostics_CrossEnvironmentDrift_ReportsWarningAsync()
    {
        // The coverage check — the point of Settex — surfaces as an editor warning.
        var doc = new SettexDocument("untitled:Untitled-1",
            "settings { App = \"x\" }\n"
            + "env \"Development\" { settings { DevOnly.Flag = true } }\n"
            + "env \"Production\" { settings { Logging.Level = \"Warn\" } }");

        await Assert.That(doc.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("DevOnly.Flag"))).IsTrue();
    }

    [Test]
    public async Task Diagnostics_ValidFile_HasNoErrorsAsync()
    {
        var doc = new SettexDocument("untitled:Untitled-1", "settings { App = \"x\" }");

        await Assert.That(doc.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)).IsFalse();
    }

    [Test]
    public async Task Diagnostics_ArrayShortenedAcrossLayers_ReportsWarningAsync()
    {
        var doc = new SettexDocument("untitled:Untitled-1",
            "settings { AllowedHosts = [\"a\", \"b\", \"c\"] }\n"
            + "env \"Production\" { settings { AllowedHosts = [\"x\"] } }");

        await Assert.That(doc.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("AllowedHosts"))).IsTrue();
    }

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
    public async Task Outline_ExcludesIncludedSymbols_ButKeepsOwnAsync()
    {
        // The document outline must list only symbols physically in this file:
        // inlined include content (with the included file's line numbers) must be
        // filtered out, while the file's own symbols are kept.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var commonPath = Path.Combine(tempDir, "common.settex");
            var mainPath = Path.Combine(tempDir, "main.settex");

            await File.WriteAllTextAsync(commonPath, "let shared = 5");
            await File.WriteAllTextAsync(
                mainPath,
                "include \"./common.settex\"\nlet localVar = 1\nsettings {\n    Port = shared\n}");

            var mainUri = DocumentUri.FromFileSystemPath(mainPath);
            var document = new SettexDocument(mainUri.ToString(), await File.ReadAllTextAsync(mainPath));

            var own = document.Ast!.Statements
                .Where(s => SettexDocument.IsFromSameFile(s.Location, mainPath))
                .ToList();

            var ownLetNames = own.OfType<LetNode>().Select(l => l.Name).ToList();

            await Assert.That(ownLetNames.Contains("localVar")).IsTrue();
            await Assert.That(ownLetNames.Contains("shared")).IsFalse();
            await Assert.That(own.OfType<SettingsBlockNode>().Any()).IsTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task IsFromSameFile_NullOrEmptyFilePath_ReturnsTrueAsync()
    {
        var noPath = new SourceLocation { Line = 1, Column = 1, Length = 1 };
        await Assert.That(SettexDocument.IsFromSameFile(noPath, @"C:\proj\main.settex")).IsTrue();

        var withPath = new SourceLocation { Line = 1, Column = 1, Length = 1, FilePath = @"C:\proj\main.settex" };
        await Assert.That(SettexDocument.IsFromSameFile(withPath, null)).IsTrue();
    }

    [Test]
    public async Task IsFromSameFile_DifferentFile_ReturnsFalseAsync()
    {
        var included = new SourceLocation { Line = 1, Column = 1, Length = 1, FilePath = @"C:\proj\common.settex" };
        await Assert.That(SettexDocument.IsFromSameFile(included, @"C:\proj\main.settex")).IsFalse();
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
