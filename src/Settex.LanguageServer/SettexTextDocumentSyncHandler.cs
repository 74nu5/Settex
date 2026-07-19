using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Settex.LanguageServer;

/// <summary>
/// Handler pour la synchronisation des documents Settex (open, change, close)
/// et publication des diagnostics.
/// </summary>
public class SettexTextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private readonly SettexWorkspace workspace;
    private readonly ILanguageServerFacade languageServer;
    private readonly ILogger<SettexTextDocumentSyncHandler> logger;
    private readonly TextDocumentSelector documentSelector = new(
        new TextDocumentFilter { Pattern = "**/*.settex" }
    );

    public SettexTextDocumentSyncHandler(
        SettexWorkspace workspace,
        ILanguageServerFacade languageServer,
        ILogger<SettexTextDocumentSyncHandler> logger)
    {
        this.workspace = workspace;
        this.languageServer = languageServer;
        this.logger = logger;
    }

    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
        new(uri, "settex");


    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        => this.GuardedAsync("didOpen", () =>
    {
        var uri = request.TextDocument.Uri.ToString();
        var text = request.TextDocument.Text;

        this.logger.LogTrace("Opened: {Uri}", uri);

        // Opening a file that other documents include makes their analysis switch
        // to this buffer, so they are refreshed and re-published too.
        foreach (var affected in this.workspace.DidOpen(uri, text))
        {
            this.PublishDiagnostics(affected.Uri, affected);
        }
    });

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        => this.GuardedAsync("didChange", () =>
    {
        var uri = request.TextDocument.Uri.ToString();

        this.logger.LogTrace("Changed: {Uri}", uri);

        // Full sync: on prend tout le nouveau texte
        if (request.ContentChanges.Any())
        {
            var newText = request.ContentChanges.First().Text;

            // Editing an included file re-analyses the documents that include it,
            // so their diagnostics stay in sync without waiting for a save.
            foreach (var affected in this.workspace.DidChange(uri, newText))
            {
                this.PublishDiagnostics(affected.Uri, affected);
            }
        }
    });

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        => this.GuardedAsync("didClose", () =>
    {
        var uri = request.TextDocument.Uri.ToString();

        this.logger.LogTrace("Closed: {Uri}", uri);

        // Documents that included this one fall back to the on-disk copy.
        foreach (var affected in this.workspace.DidClose(uri))
        {
            this.PublishDiagnostics(affected.Uri, affected);
        }

        // Efface les diagnostics
        this.languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });
    });

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        => this.GuardedAsync("didSave", () => this.logger.LogTrace("Saved: {Uri}", request.TextDocument.Uri));

    /// <summary>
    /// Runs a notification's body, degrading to a logged error instead of faulting the
    /// notification. Parsing a URI or publishing diagnostics can throw, and a faulted
    /// didChange leaves the client without a reply and the document unanalysed — the
    /// other handlers were guarded for exactly this reason; these four were missed.
    /// </summary>
    private Task<Unit> GuardedAsync(string operation, Action body)
    {
        try
        {
            body();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{Operation} failed", operation);
        }

        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = this.documentSelector,
            Change = this.Change,
            Save = new SaveOptions { IncludeText = false }
        };

    /// <summary>
    /// Publie les diagnostics pour un document.
    /// </summary>
    private void PublishDiagnostics(string uri, SettexDocument document)
    {
        this.languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = DocumentUri.From(uri),
            Diagnostics = new Container<Diagnostic>(document.Diagnostics)
        });
    }
}
