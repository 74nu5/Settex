using MediatR;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Progress;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Settex.LanguageServer.Tests;

/// <summary>
/// A language-server facade that records the diagnostics published to it instead of
/// writing them to a client. The notification handlers take the real facade type, so
/// testing what they publish — and in what order — needs a stand-in for it.
///
/// <para>
/// Only the text-document channel is implemented: everything else throws, so a handler
/// that starts using another channel fails loudly here rather than passing silently.
/// </para>
/// </summary>
internal sealed class RecordingLanguageServer : ILanguageServerFacade, ITextDocumentLanguageServer
{
    private readonly List<(string Uri, int Count)> published = [];

    public ITextDocumentLanguageServer TextDocument => this;

    /// <summary>Every publish, in order: the target URI and how many diagnostics it carried.</summary>
    public IReadOnlyList<(string Uri, int Count)> Published => this.published;

    public INotebookDocumentLanguageServer NotebookDocument => throw new NotSupportedException();

    public IClientLanguageServer Client => throw new NotSupportedException();

    public IGeneralLanguageServer General => throw new NotSupportedException();

    public IWindowLanguageServer Window => throw new NotSupportedException();

    public IWorkspaceLanguageServer Workspace => throw new NotSupportedException();

    public IProgressManager ProgressManager => throw new NotSupportedException();

    public InitializeParams ClientSettings => throw new NotSupportedException();

    public InitializeResult ServerSettings => throw new NotSupportedException();

    public void SendNotification(string method) => throw new NotSupportedException();

    public void SendNotification<T>(string method, T @params)
    {
        if (@params is PublishDiagnosticsParams diagnostics)
        {
            this.published.Add((diagnostics.Uri.ToString(), diagnostics.Diagnostics.Count()));
            return;
        }

        throw new NotSupportedException($"Unexpected notification '{method}'");
    }

    public void SendNotification(IRequest request)
    {
        // PublishDiagnostics routes through this overload, not the generic one.
        if (request is PublishDiagnosticsParams diagnostics)
        {
            this.published.Add((diagnostics.Uri.ToString(), diagnostics.Diagnostics.Count()));
            return;
        }

        throw new NotSupportedException($"Unexpected notification '{request.GetType().Name}'");
    }

    public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task SendRequest(IRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public IResponseRouterReturns SendRequest<T>(string method, T @params) => throw new NotSupportedException();

    public IResponseRouterReturns SendRequest(string method) => throw new NotSupportedException();

    public bool TryGetRequest(
        long id,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? method,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TaskCompletionSource<JToken>? pendingTask)
        => throw new NotSupportedException();

    public IDisposable Register(Action<ILanguageServerRegistry> registryAction) => throw new NotSupportedException();

    public object? GetService(Type serviceType) => throw new NotSupportedException();
}
