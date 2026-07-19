using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// Coverage and array-layering warnings are produced from the evaluated JSON model,
/// which has no source positions. Every one of them therefore landed on the first
/// character of the file — a stack of warnings on one spot, none pointing at what to
/// change — and, because the model is built from the include-flattened AST, every
/// including file republished its includes' warnings verbatim.
/// </summary>
public sealed class DiagnosticAnchoringTests
{
    [Test]
    public async Task CoverageWarning_AnchorsOnTheAssignmentThatIntroducedTheKeyAsync()
    {
        // Feature.NewCheckout is on line 7 (0-based 6).
        const string source = """
            settings {
                App = "X"
            }
            env "Dev" {
                settings {
                    App = "Y"
                    Feature.NewCheckout = true
                }
            }
            env "Prod" {
                settings {
                    App = "Z"
                }
            }
            """;

        var document = new SettexDocument("untitled:anchor", source);
        var warning = document.Diagnostics.Single(d => d.Message.Contains("Feature.NewCheckout"));

        await Assert.That(warning.Range.Start.Line).IsEqualTo(6);
        await Assert.That(warning.Range.Start.Character).IsGreaterThan(0);
    }

    [Test]
    public async Task CoverageWarning_ForAKeyInsideNestedBlocks_AnchorsOnTheLeafAssignmentAsync()
    {
        // The analyzer reports the dotted path, so the search has to accumulate block
        // names to match it. Logging.Level is on line 7 (0-based 6).
        const string source = """
            settings {
                App = "X"
            }
            env "Dev" {
                settings {
                    Logging {
                        Level = "Debug"
                    }
                }
            }
            env "Prod" {
                settings {
                    Other = 1
                }
            }
            """;

        var document = new SettexDocument("untitled:nested-anchor", source);
        var warning = document.Diagnostics.Single(d => d.Message.Contains("Logging.Level"));

        await Assert.That(warning.Range.Start.Line).IsEqualTo(6);
    }

    [Test]
    public async Task LayeringWarning_AnchorsOnTheOverridingArrayAsync()
    {
        // Hosts is overridden on line 7 (0-based 6).
        const string source = """
            settings {
                Hosts = ["a", "b", "c"]
            }
            env "Dev" {
                settings {
                    Hosts = ["x"]
                }
            }
            """;

        var document = new SettexDocument("untitled:layering-anchor", source);
        var warning = document.Diagnostics.Single(d => d.Message.Contains("layers"));

        await Assert.That(warning.Range.Start.Line).IsEqualTo(5);
    }

    /// <summary>
    /// This originally asserted the opposite: that an includer stays quiet because the
    /// file owning the key reports it instead. That reasoning had a hole — a fragment
    /// with no <c>settings</c> block never runs the analyzers, so splitting a file into
    /// an include deleted the warning from the editor entirely. A warning duplicated
    /// across includer and include is noise; a missing one is a defect, and drift is
    /// the thing this tool exists to catch.
    /// </summary>
    [Test]
    public async Task IncludedDrift_IsStillReportedOnTheIncluderAsync()
    {
        using var temp = new TempDirectory();

        // The drift lives entirely in the included file.
        var libPath = temp.Write(
            "lib.settex",
            """
            settings {
                App = "X"
            }
            env "Dev" {
                settings {
                    OnlyDev = true
                }
            }
            env "Prod" {
                settings {
                    App = "Z"
                }
            }
            """);

        var mainPath = temp.Write("main.settex", "include \"./lib.settex\"\nsettings {\n    Extra = 1\n}");

        var lib = new SettexDocument(DocumentUri.FromFileSystemPath(libPath).ToString(), File.ReadAllText(libPath));
        var main = new SettexDocument(DocumentUri.FromFileSystemPath(mainPath).ToString(), File.ReadAllText(mainPath));

        // The file that defines the key reports it, anchored on the assignment.
        var inLib = lib.Diagnostics.Single(d => d.Message.Contains("OnlyDev"));

        await Assert.That(inLib.Range.Start.Line).IsGreaterThan(0);

        // And so does the includer, at the file start for want of a local assignment.
        // Reporting it twice costs a duplicate; not reporting it lost it altogether.
        await Assert.That(main.Diagnostics.Any(d => d.Message.Contains("OnlyDev"))).IsTrue();
    }

    [Test]
    public async Task DriftInASettingsLessFragment_IsReportedByTheRootAsync()
    {
        // The hole the change closes. The fragment holds only env blocks, so it never
        // runs the analyzers; the root used to drop the warning for want of a local
        // anchor. Between them, splitting a file into an include deleted it.
        using var temp = new TempDirectory();

        temp.Write(
            "frag.settex",
            """
            env "Dev" {
                settings {
                    Feature.NewCheckout = true
                }
            }
            env "Prod" {
                settings {
                    App = "Z"
                }
            }
            """);

        var rootPath = temp.Write("root.settex", "include \"./frag.settex\"\nsettings {\n    App = \"X\"\n}");
        var root = new SettexDocument(DocumentUri.FromFileSystemPath(rootPath).ToString(), File.ReadAllText(rootPath));

        await Assert.That(root.Diagnostics.Any(d => d.Message.Contains("Feature.NewCheckout"))).IsTrue();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"settex-anchor-{Guid.NewGuid():N}");
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
