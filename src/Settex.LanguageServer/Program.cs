using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using Settex.LanguageServer;

namespace Settex.LanguageServer;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .ConfigureLogging(x => x
                    .AddLanguageProtocolLogging()
                    // Quiet by default: the verbose per-request instrumentation
                    // logs at Trace and only surfaces when the client raises the
                    // trace level.
                    .SetMinimumLevel(LogLevel.Information))
                .WithServices(services =>
                {
                    // Register workspace as singleton
                    services.AddSingleton<SettexWorkspace>();
                })
                .WithHandler<SettexTextDocumentSyncHandler>()
                .WithHandler<SettexCompletionHandler>()
                .WithHandler<SettexHoverHandler>()
                .WithHandler<SettexDefinitionHandler>()
                .WithHandler<SettexReferencesHandler>()
                .WithHandler<SettexDocumentSymbolHandler>()
        ).ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }
}
