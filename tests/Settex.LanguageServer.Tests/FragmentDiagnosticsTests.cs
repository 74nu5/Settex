using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// The CLI only ever compiles a root file, so "a file must contain at least one
/// settings block" is a rule about compilation roots. The editor, however, opens
/// whatever the user opens — and the server used to evaluate every buffer as if it
/// were a root, putting a permanent error on include fragments. The repository ships
/// one such fragment, and the README presents the pattern as the normal way to build
/// modular configuration.
/// </summary>
public sealed class FragmentDiagnosticsTests
{
    [Test]
    public async Task Parse_IncludeFragmentWithNoSettingsBlock_ReportsNoDiagnosticAsync()
    {
        // The shape of samples/common.settex: variables only, meant to be included.
        const string fragment = """
            let defaultPort = 5000
            let logLevel = "Information"
            """;

        var document = new SettexDocument("untitled:fragment", fragment);

        await Assert.That(document.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task Parse_EmptyDocument_ReportsNoDiagnosticAsync()
    {
        // A freshly created .settex file is not an error either; it is just empty.
        var document = new SettexDocument("untitled:empty", string.Empty);

        await Assert.That(document.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task Parse_FragmentDeclaringEnvironments_DoesNotReportCoverageDriftAsync()
    {
        // Drift analysis compares environments against each other, which only makes
        // sense with the whole picture. In a fragment the missing environment may be
        // declared by the including file, so the comparison would invent a problem.
        const string fragment = """
            env "Dev" {
                settings {
                    OnlyHere = 1
                }
            }
            env "Prod" {
                settings {
                    Other = 2
                }
            }
            """;

        var document = new SettexDocument("untitled:env-fragment", fragment);

        await Assert.That(document.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task Parse_RealErrorInsideAFragment_IsStillReportedAsync()
    {
        // Relaxing the structural rule must not silence genuine semantic errors:
        // dropping evaluation altogether would have been the lazy fix.
        const string fragment = "let broken = undefinedVariable";

        var document = new SettexDocument("untitled:broken-fragment", fragment);

        await Assert.That(document.Diagnostics).IsNotEmpty();
        await Assert.That(document.Diagnostics[0].Message).Contains("undefinedVariable");
    }

    [Test]
    public async Task Parse_RootFileWithSettings_StillReportsCoverageDriftAsync()
    {
        // The guard in the other direction: a real root must keep its drift warning,
        // otherwise the fix would have disabled the product's flagship check.
        const string root = """
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
                    App = "Y"
                }
            }
            """;

        var document = new SettexDocument("untitled:root", root);

        await Assert.That(document.Diagnostics).IsNotEmpty();
        await Assert.That(document.Diagnostics.Any(d => d.Message.Contains("OnlyDev"))).IsTrue();
    }

    [Test]
    public async Task Parse_ShippedCommonSample_ReportsNoDiagnosticAsync()
    {
        // The concrete regression: samples/common.settex, as shipped, showed a red
        // squiggle when opened. Reads the real file so the sample and the server
        // cannot drift apart.
        var samplePath = FindRepositoryFile(Path.Combine("samples", "common.settex"));

        if (samplePath == null)
        {
            // Running from a packaged layout without the repository around.
            return;
        }

        var uri = DocumentUri.FromFileSystemPath(samplePath);
        var document = new SettexDocument(uri.ToString(), await File.ReadAllTextAsync(samplePath));

        await Assert.That(document.Diagnostics).IsEmpty();
    }

    private static string? FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
