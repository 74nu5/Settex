using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Settex.Core.Parser.Ast;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// The AST the server works on is include-flattened: statements from included files
/// are merged into the current document's. Position lookups used to compare line and
/// column only, so lines 5-10 of an included file shadowed lines 5-10 of the file
/// actually open in the editor — hover, go-to-definition and find-references all
/// answered from the wrong file. These tests pin each lookup to the right one.
///
/// Every case here is built so the two files <em>collide deliberately</em>: the
/// interesting symbol sits at the same line and column in both. Without that
/// alignment the tests would pass even with the defect present.
/// </summary>
public sealed class CrossFilePositionTests
{
    [Test]
    public async Task FindScopeAt_PositionCoveredByAnIncludedEnvBlock_StaysInTheDocumentScopeAsync()
    {
        // The included env block spans lines 4-9. In main.settex those same lines are
        // plain global-scope territory, so the cursor must land on Global — and the
        // `host` it sees must be main's, not the env-scoped one from the include.
        const string library = """
            settings {
                A = 1
            }
            env "Dev" {
                let host = "DEV-HOST"
                settings {
                    B = host
                }
            }
            """;

        // Line 8 is `C = host`, inside the included env's line range.
        const string main = """
            include "./library.settex"

            let host = "MAIN-HOST"

            settings {
                A = 1
                B = 2
                C = host
            }
            """;

        using var workspace = new TempWorkspace(library, main);
        var document = workspace.OpenMain();
        var resolver = new ScopeResolver();

        var rootScope = resolver.BuildScopeHierarchy(document.Ast!);
        var activeScope = resolver.FindScopeAt(rootScope, Position(8, 8), document.FilePath);

        await Assert.That(activeScope).IsNotNull();
        await Assert.That(activeScope!.Type).IsEqualTo(ScopeType.Global);

        var resolved = resolver.FindVariableInScope("host", activeScope);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(Path.GetFileName(resolved!.Location.FilePath!)).IsEqualTo("main.settex");
    }

    [Test]
    public async Task FindScopeAt_PositionInsideTheDocumentOwnEnvBlock_StillResolvesToItAsync()
    {
        // The mirror image: the filter must not make the document's own env scopes
        // unreachable. Without this, the fix above could "pass" by never descending.
        const string library = "settings {\n    A = 1\n}";

        const string main = """
            include "./library.settex"
            settings {
                A = 1
            }
            env "Dev" {
                let host = "DEV-HOST"
                settings {
                    B = host
                }
            }
            """;

        using var workspace = new TempWorkspace(library, main);
        var document = workspace.OpenMain();
        var resolver = new ScopeResolver();

        var rootScope = resolver.BuildScopeHierarchy(document.Ast!);

        // Line 8 is `B = host`, inside main's own env block.
        var activeScope = resolver.FindScopeAt(rootScope, Position(8, 8), document.FilePath);

        await Assert.That(activeScope).IsNotNull();
        await Assert.That(activeScope!.Type).IsEqualTo(ScopeType.Env);
        await Assert.That(activeScope.Name).IsEqualTo("Dev");
        await Assert.That(resolver.FindVariableInScope("host", activeScope)!.Value).IsNotNull();
    }

    [Test]
    public async Task BuildScopeHierarchy_GlobalLetFromAnIncludedFile_RemainsVisibleAsync()
    {
        // The invariant the fix must not break. The compiler makes a file-level `let`
        // from an included file visible in the including file, so the server has to
        // as well — filtering positions must not filter visibility.
        const string library = "let shared = \"from-include\"\nsettings {\n    A = 1\n}";
        const string main = "include \"./library.settex\"\nsettings {\n    B = shared\n}";

        using var workspace = new TempWorkspace(library, main);
        var document = workspace.OpenMain();
        var resolver = new ScopeResolver();

        var rootScope = resolver.BuildScopeHierarchy(document.Ast!);
        var activeScope = resolver.FindScopeAt(rootScope, Position(3, 9), document.FilePath);

        var resolved = resolver.FindVariableInScope("shared", activeScope!);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(Path.GetFileName(resolved!.Location.FilePath!)).IsEqualTo("library.settex");
    }

    [Test]
    public async Task Hover_OnAnAssignmentCollidingWithAnIncludedOne_ReportsTheDocumentValueAsync()
    {
        // `Zeta` sits at the very same line and column in both files. Included
        // statements are prepended, so the included one used to win the search and the
        // overlay reported 111 for a key whose value here is 999.
        // Indentation is flush left on purpose: the two `Zeta` must share a column as
        // well as a line, otherwise the span check separates them and the test proves
        // nothing.
        const string library = """
            settings {
            Alpha {

            Zeta = 111
            }
            }
            """;

        const string main = """
            include "./library.settex"
            settings {

            Zeta = 999
            }
            """;

        using var workspace = new TempWorkspace(library, main);
        var handler = workspace.OpenMainForHover();

        // Cursor on `Zeta` at line 4, column 1 (0-based: 3, 0).
        var hover = await handler.Handle(HoverRequest(workspace.MainUri, 3, 0), CancellationToken.None);

        await Assert.That(hover).IsNotNull();

        var content = hover!.Contents.MarkupContent!.Value;

        await Assert.That(content).Contains("999");
        await Assert.That(content).DoesNotContain("111");
    }

    private static Position Position(int line, int column) => new(line - 1, column - 1);

    private static HoverParams HoverRequest(DocumentUri uri, int line, int character) => new()
    {
        TextDocument = new TextDocumentIdentifier { Uri = uri },
        Position = new Position(line, character),
    };

    /// <summary>
    /// A throwaway directory holding a <c>main.settex</c> that includes a
    /// <c>library.settex</c>. Real files on disk are required: include resolution and
    /// the file comparison both work off <see cref="SourceLocation.FilePath" />, which
    /// only exists for a document backed by a path.
    /// </summary>
    private sealed class TempWorkspace : IDisposable
    {
        private readonly string directory;
        private readonly string mainPath;
        private readonly string mainText;
        private readonly SettexWorkspace workspace = new();

        public TempWorkspace(string libraryText, string mainText)
        {
            this.directory = Path.Combine(Path.GetTempPath(), $"settex-xfile-{Guid.NewGuid():N}");
            Directory.CreateDirectory(this.directory);

            File.WriteAllText(Path.Combine(this.directory, "library.settex"), libraryText);

            this.mainPath = Path.Combine(this.directory, "main.settex");
            this.mainText = mainText;
            File.WriteAllText(this.mainPath, mainText);

            this.MainUri = DocumentUri.FromFileSystemPath(this.mainPath);
        }

        public DocumentUri MainUri { get; }

        public SettexDocument OpenMain() => new(this.MainUri.ToString(), this.mainText);

        public SettexHoverHandler OpenMainForHover()
        {
            this.workspace.DidOpen(this.MainUri.ToString(), this.mainText);

            return new SettexHoverHandler(this.workspace, NullLogger<SettexHoverHandler>.Instance);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.directory, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory must never fail a test run.
            }
        }
    }
}
