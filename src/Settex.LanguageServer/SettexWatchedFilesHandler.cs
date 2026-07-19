using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

// Disambiguates from System.IO.FileSystemWatcher: this one is the LSP registration
// model, not an actual watcher — the client does the watching.
using FileSystemWatcher = OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher;

namespace Settex.LanguageServer;

/// <summary>
/// Re-analyses open documents when a <c>.settex</c> file changes <strong>on disk</strong>.
///
/// <para>
/// Resolving includes against open buffers only covers edits made in this editor. A
/// <c>git checkout</c>, a branch switch, a build step or an edit from another tool
/// changes a file no buffer knows about — and the client already watches
/// <c>**/*.settex</c> for us. Without this handler that notification arrived and was
/// dropped, so every document including the changed file kept serving stale analysis
/// indefinitely.
/// </para>
///
/// <para>
/// A file that is open in the editor is deliberately skipped: its buffer is the
/// authority, and re-reading the disk copy would discard unsaved edits.
/// </para>
/// </summary>
public class SettexWatchedFilesHandler : DidChangeWatchedFilesHandlerBase
{
    private readonly SettexWorkspace workspace;
    private readonly ILanguageServerFacade languageServer;
    private readonly ILogger<SettexWatchedFilesHandler> logger;

    public SettexWatchedFilesHandler(
        SettexWorkspace workspace,
        ILanguageServerFacade languageServer,
        ILogger<SettexWatchedFilesHandler> logger)
    {
        this.workspace = workspace;
        this.languageServer = languageServer;
        this.logger = logger;
    }

    /// <summary>
    /// Degrades to "no refresh" instead of faulting the notification.
    /// </summary>
    public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var change in request.Changes)
            {
                var filePath = change.Uri.GetFileSystemPath();

                if (string.IsNullOrEmpty(filePath))
                {
                    continue;
                }

                foreach (var document in this.workspace.RefreshDependentsOf(filePath))
                {
                    this.languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
                    {
                        Uri = DocumentUri.From(document.Uri),
                        Diagnostics = new Container<Diagnostic>(document.Diagnostics),
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Refreshing after a watched-file change failed");
        }

        return Unit.Task;
    }

    protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(
        DidChangeWatchedFilesCapability capability,
        ClientCapabilities clientCapabilities)
        => new()
        {
            Watchers = new Container<FileSystemWatcher>(
                new FileSystemWatcher
                {
                    GlobPattern = new GlobPattern("**/*.settex"),
                    Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete,
                }),
        };
}
