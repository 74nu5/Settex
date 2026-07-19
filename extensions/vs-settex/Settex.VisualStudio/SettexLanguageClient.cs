namespace Settex.VisualStudio;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

/// <summary>
/// Language Server Protocol client for Settex.
/// Connects to the Settex.LanguageServer to provide IntelliSense and diagnostics.
/// </summary>
[ContentType("settex")]
[Export(typeof(ILanguageClient))]
public class SettexLanguageClient : ILanguageClient, IDisposable
{
    private Process? serverProcess;

    /// <summary>
    /// Gets the name of the language client.
    /// </summary>
    public string Name => "Settex Language Client";

    /// <summary>
    /// Gets the configuration of the language client.
    /// </summary>
    public IEnumerable<string> ConfigurationSections => Array.Empty<string>();

    /// <summary>
    /// Gets additional initialization options.
    /// </summary>
    public object InitializationOptions => null!;

    /// <summary>
    /// Gets the list of file names to monitor.
    /// </summary>
    public IEnumerable<string> FilesToWatch => Array.Empty<string>();

    /// <summary>
    /// Gets a value indicating whether to show a notification to the user when initialization fails.
    /// </summary>
    public bool ShowNotificationOnInitializeFailed => true;

    /// <summary>
    /// Event raised when the language server is starting.
    /// </summary>
    public event AsyncEventHandler<EventArgs>? StartAsync;

    // StopAsync is required by ILanguageClient but is never raised by this client;
    // the server lifecycle is owned by Visual Studio. Suppress the unused-event warning.
#pragma warning disable CS0067
    /// <summary>
    /// Event raised when the language server has stopped.
    /// </summary>
    public event AsyncEventHandler<EventArgs>? StopAsync;
#pragma warning restore CS0067

    /// <summary>
    /// Activates the language client.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    public async Task<Connection?> ActivateAsync(CancellationToken token)
    {
        await Task.Yield();

        // Find the Settex.LanguageServer executable
        var serverPath = this.GetLanguageServerPath();

        if (string.IsNullOrEmpty(serverPath))
        {
            // Language server not found - return null to disable LSP features
            Debug.WriteLine("Settex Language Server not found. IntelliSense features will be disabled.");
            return null;
        }

        // The server is a .NET 10 app. Check the runtime up front so a missing
        // runtime yields an actionable message instead of an opaque start failure.
        if (!DotNetRuntime.IsAvailable(out var runtimeDetail))
        {
            Debug.WriteLine($"Settex Language Server disabled - .NET 10 runtime unavailable: {runtimeDetail}");
            await this.ShowRuntimeMissingMessageAsync(runtimeDetail);
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

        Process? process;

        try
        {
            process = Process.Start(processStartInfo);
        }
        catch (Exception ex)
        {
            // 'dotnet' not found or the process could not be started: disable LSP
            // features gracefully rather than letting the exception surface.
            Debug.WriteLine($"Failed to start Settex Language Server (is the .NET runtime installed?): {ex.Message}");
            return null;
        }

        if (process == null)
        {
            return null;
        }

        // Keep a handle so the server can be terminated on shutdown, and clear it
        // when the process exits on its own.
        this.serverProcess = process;
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            if (ReferenceEquals(this.serverProcess, process))
            {
                this.serverProcess = null;
            }
        };

        // stderr is redirected, so it MUST be drained continuously: a server that
        // writes to it would otherwise fill the OS pipe buffer and block. Forward
        // each line to the debug output.
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Debug.WriteLine($"[Settex LSP] {e.Data}");
            }
        };
        process.BeginErrorReadLine();

        return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
    }

    /// <summary>
    /// Shows an actionable message when the .NET 10 runtime the server needs is
    /// missing, offering to open the download page. Syntax highlighting keeps
    /// working; only the language-server features are disabled.
    /// </summary>
    /// <param name="detail">Human-readable reason from the runtime check.</param>
    private async Task ShowRuntimeMissingMessageAsync(string detail)
    {
        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var message =
                "Settex IntelliSense could not start because the .NET 10 runtime is unavailable.\n\n" +
                $"{detail}\n\n" +
                "Syntax highlighting and snippets still work. Click OK to open the .NET 10 download page, " +
                "then reload the window once it is installed.";

            var result = VsShellUtilities.ShowMessageBox(
                ServiceProvider.GlobalProvider,
                message,
                "Settex Language Server",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            const int idok = 1; // Win32 IDOK

            if (result == idok)
            {
                Process.Start(new ProcessStartInfo(DotNetRuntime.DownloadUrl) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            // Never let a notification failure break activation.
            Debug.WriteLine($"Failed to show the .NET runtime message: {ex.Message}");
        }
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
    /// <param name="initializationState">Information about the initialization failure.</param>
    /// <returns>Task producing the failure context, or null to use default handling.</returns>
    public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
    {
        var message = initializationState.InitializationException?.Message ?? initializationState.StatusMessage;
        Debug.WriteLine($"Settex Language Server initialization failed: {message}");

        var failureContext = new InitializationFailureContext
        {
            FailureMessage = $"Settex Language Server failed to initialize: {message}",
        };

        return Task.FromResult<InitializationFailureContext?>(failureContext);
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
    /// <returns>Path to the language server DLL, or empty string if not found.</returns>
    private string GetLanguageServerPath()
    {
        // The extension assembly directory (not AppDomain.BaseDirectory, which is
        // the Visual Studio process directory for an installed extension).
        var extensionDir = Path.GetDirectoryName(typeof(SettexLanguageClient).Assembly.Location) ?? string.Empty;

        // Try to find the language server in several locations
        var searchPaths = new[]
        {
            // Installation location (bundled inside the VSIX)
            Path.Combine(extensionDir, "LanguageServer", "Settex.LanguageServer.dll"),

            // Development location (when running from source: bin\<cfg>\net472 -> repo root)
            Path.GetFullPath(Path.Combine(extensionDir, "..", "..", "..", "..", "..", "..", "src", "Settex.LanguageServer", "bin", "Debug", "net10.0", "Settex.LanguageServer.dll")),

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

        return string.Empty;
    }

    /// <summary>
    /// Searches for a file in the system PATH.
    /// </summary>
    /// <param name="fileName">File name to search for.</param>
    /// <returns>Full path if found, or empty string.</returns>
    private string FindInPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return string.Empty;
        }

        var paths = pathEnv.Split(Path.PathSeparator);
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
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

        return string.Empty;
    }

    /// <summary>
    /// Terminates the language server process if it is still running. Prevents an
    /// orphaned 'dotnet' process when the client is torn down or Visual Studio exits.
    /// </summary>
    public void Dispose()
    {
        var process = this.serverProcess;
        this.serverProcess = null;

        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch
        {
            // Process already exited or could not be killed; nothing to do.
        }
        finally
        {
            process.Dispose();
        }
    }
}
