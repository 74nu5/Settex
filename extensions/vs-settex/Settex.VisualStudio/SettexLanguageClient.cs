namespace Settex.VisualStudio;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

/// <summary>
/// Language Server Protocol client for Settex.
/// Connects to the Settex.LanguageServer to provide IntelliSense and diagnostics.
/// </summary>
[ContentType("settex")]
[Export(typeof(ILanguageClient))]
public class SettexLanguageClient : ILanguageClient
{
    /// <summary>
    /// Gets the name of the language client.
    /// </summary>
    public string Name => "Settex Language Client";

    /// <summary>
    /// Gets the configuration of the language client.
    /// </summary>
    public IEnumerable<string> ConfigurationSections => null;

    /// <summary>
    /// Gets additional initialization options.
    /// </summary>
    public object InitializationOptions => null;

    /// <summary>
    /// Gets the list of file names to monitor.
    /// </summary>
    public IEnumerable<string> FilesToWatch => null;

    /// <summary>
    /// Event raised when the language server has stopped.
    /// </summary>
    public event AsyncEventHandler<EventArgs> StartAsync;

    /// <summary>
    /// Event raised when the language server has stopped.
    /// </summary>
    public event AsyncEventHandler<EventArgs> StopAsync;

    /// <summary>
    /// Activates the language client.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    public async Task<Connection> ActivateAsync(CancellationToken token)
    {
        await Task.Yield();

        // Find the Settex.LanguageServer executable
        var serverPath = this.GetLanguageServerPath();

        if (serverPath == null || !File.Exists(serverPath))
        {
            // Language server not found - return null to disable LSP features
            Debug.WriteLine("Settex Language Server not found. IntelliSense features will be disabled.");
            return null;
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{serverPath}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = Process.Start(processStartInfo);

        if (process == null)
        {
            return null;
        }

        return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
    }

    /// <summary>
    /// Called when the language server is loaded.
    /// </summary>
    /// <returns>Task representing the async operation.</returns>
    public async Task OnLoadedAsync()
    {
        if (this.StartAsync != null)
        {
            await this.StartAsync.InvokeAsync(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Called when the server initialization has failed.
    /// </summary>
    /// <param name="exception">Exception that caused the failure.</param>
    /// <returns>Task representing the async operation.</returns>
    public Task OnServerInitializeFailedAsync(Exception exception)
    {
        Debug.WriteLine($"Settex Language Server initialization failed: {exception.Message}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the server initialization has succeeded.
    /// </summary>
    /// <returns>Task representing the async operation.</returns>
    public Task OnServerInitializedAsync()
    {
        Debug.WriteLine("Settex Language Server initialized successfully.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Attempts to find the Settex.LanguageServer executable.
    /// Searches in common locations relative to the extension.
    /// </summary>
    /// <returns>Path to the language server DLL, or null if not found.</returns>
    private string GetLanguageServerPath()
    {
        // Try to find the language server in several locations
        var searchPaths = new[]
        {
            // Development location (when running from source)
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..", "src", "Settex.LanguageServer", "bin", "Debug", "net10.0", "Settex.LanguageServer.dll")),
            
            // Installation location (when installed as VSIX)
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LanguageServer", "Settex.LanguageServer.dll"),
            
            // Look in PATH environment variable for global installation
            this.FindInPath("Settex.LanguageServer.dll"),
        };

        foreach (var path in searchPaths)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                Debug.WriteLine($"Found Settex Language Server at: {path}");
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Searches for a file in the system PATH.
    /// </summary>
    /// <param name="fileName">File name to search for.</param>
    /// <returns>Full path if found, or null.</returns>
    private string FindInPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        var paths = pathEnv.Split(Path.PathSeparator);
        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Ignore invalid paths
            }
        }

        return null;
    }
}
