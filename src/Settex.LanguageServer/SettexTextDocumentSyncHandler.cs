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
        ILanguageServerFacade languageServer,
        ILogger<SettexTextDocumentSyncHandler> logger)
    {
        this.workspace = new SettexWorkspace();
        this.languageServer = languageServer;
        this.logger = logger;
    }

    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
        new(uri, "settex");


    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var text = request.TextDocument.Text;

        this.logger.LogInformation("Opened: {Uri}", uri);

        var document = this.workspace.DidOpen(uri, text);
        this.PublishDiagnostics(uri, document);

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();

        this.logger.LogInformation("Changed: {Uri}", uri);

        // Full sync: on prend tout le nouveau texte
        if (request.ContentChanges.Any())
        {
            var newText = request.ContentChanges.First().Text;
            var document = this.workspace.DidChange(uri, newText);

            if (document != null)
            {
                this.PublishDiagnostics(uri, document);
            }
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();

        this.logger.LogInformation("Closed: {Uri}", uri);

        this.workspace.DidClose(uri);

        // Efface les diagnostics
        this.languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Saved: {Uri}", request.TextDocument.Uri);
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
