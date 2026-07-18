namespace Settex.VisualStudio;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// Service for compiling Settex files during build.
/// </summary>
internal class SettexBuildService : ISettexBuildService
{
    /// <summary>
    /// Maximum time a single .settex compilation may run before it is cancelled.
    /// </summary>
    private const int CompilationTimeoutMs = 60000;

    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettexBuildService"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider.</param>
    public SettexBuildService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Compiles a Settex file to appsettings.json.
    /// </summary>
    /// <param name="settexFilePath">Path to the .settex file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="showDialogs">Whether to show error/warning dialogs.</param>
    /// <returns>True if compilation succeeded, false otherwise.</returns>
    public async Task<bool> CompileSettexFileAsync(string settexFilePath, CancellationToken cancellationToken, bool showDialogs = true)
    {
        if (string.IsNullOrEmpty(settexFilePath) || !File.Exists(settexFilePath))
        {
            return false;
        }

        try
        {
            await Task.Yield();

            // Find the Settex.Cli tool
            var cliPath = this.FindSettexCli();

            if (string.IsNullOrEmpty(cliPath))
            {
                Debug.WriteLine("Settex.Cli not found. Cannot compile .settex files.");
                if (showDialogs)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    this.ShowWarning("Settex compiler not found. Please ensure Settex.Cli is installed.");
                }
                return false;
            }

            // Get the directory of the settex file; fall back to current directory if none
            var directory = Path.GetDirectoryName(settexFilePath) ?? Directory.GetCurrentDirectory();

            // Run the Settex compiler
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = SettexCliInvocation.BuildCompileArguments(cliPath, settexFilePath),
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // Bound the compilation: cancel it if the caller cancels or the timeout elapses.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(CompilationTimeoutMs);

            using (var process = Process.Start(processStartInfo))
            {
                if (process == null)
                {
                    return false;
                }

                // Kill the external dotnet process on cancel/timeout; otherwise cancelling
                // only the wait would leave the process running in the background.
                using (timeoutCts.Token.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception killEx)
                    {
                        // Process already exited or could not be killed. Log it: a genuine
                        // kill failure is the only way WaitForExit could hang indefinitely.
                        Debug.WriteLine($"Settex: failed to kill compiler process: {killEx.Message}");
                    }
                }))
                {
                    // Start reading both streams before waiting to avoid a full-buffer deadlock.
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // WaitForExitAsync is not available on net472; wait on a background thread.
                    // The registration above makes the process exit promptly on cancel/timeout.
                    await Task.Run(() => process.WaitForExit());

                    var output = await outputTask;
                    var error = await errorTask;

                    // Distinguish caller cancellation (expected) from a timeout.
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    if (timeoutCts.IsCancellationRequested)
                    {
                        Debug.WriteLine($"Settex compilation timed out after {CompilationTimeoutMs} ms.");
                        if (showDialogs)
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            this.ShowError($"Settex compilation timed out after {CompilationTimeoutMs / 1000} seconds.");
                        }
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        Debug.WriteLine($"Settex compilation failed: {error}");
                        if (showDialogs)
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            this.ShowError($"Settex compilation failed:\n{error}");
                        }
                        return false;
                    }

                    Debug.WriteLine($"Settex compilation succeeded: {output}");
                    return true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected when a newer save supersedes this compilation;
            // let the caller handle it instead of reporting an error.
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error compiling Settex file: {ex.Message}");
            if (showDialogs)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                this.ShowError($"Error compiling Settex file: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Compiles all Settex files in a project.
    /// </summary>
    /// <param name="projectPath">Path to the project directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all compilations succeeded, false otherwise.</returns>
    public async Task<bool> CompileProjectSettexFilesAsync(string projectPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
        {
            return false;
        }

        var settexFiles = Directory.GetFiles(projectPath, "*.settex", SearchOption.AllDirectories);

        if (settexFiles.Length == 0)
        {
            return true; // No files to compile
        }

        var allSucceeded = true;

        foreach (var file in settexFiles)
        {
            var succeeded = await this.CompileSettexFileAsync(file, cancellationToken);
            allSucceeded = allSucceeded && succeeded;

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        return allSucceeded;
    }

    /// <summary>
    /// Attempts to find the Settex.Cli executable.
    /// </summary>
    /// <returns>Path to the CLI DLL, or empty string if not found.</returns>
    private string FindSettexCli()
    {
        // The extension assembly directory (not AppDomain.BaseDirectory, which is
        // the Visual Studio process directory for an installed extension).
        var extensionDir = Path.GetDirectoryName(typeof(SettexBuildService).Assembly.Location) ?? string.Empty;

        var searchPaths = new[]
        {
            // Installation location (bundled inside the VSIX)
            Path.Combine(extensionDir, "Cli", "Settex.Cli.dll"),

            // Development location (bin\<cfg>\net472 -> repo root)
            Path.GetFullPath(Path.Combine(extensionDir, "..", "..", "..", "..", "..", "..", "src", "Settex.Cli", "bin", "Debug", "net10.0", "Settex.Cli.dll")),

            // Look in PATH for global tool
            this.FindInPath("Settex.Cli.dll"),
        };

        foreach (var path in searchPaths)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
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
    /// Shows an error message to the user.
    /// </summary>
    /// <param name="message">Error message.</param>
    private void ShowError(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        VsShellUtilities.ShowMessageBox(
            this.serviceProvider,
            message,
            "Settex Build Error",
            OLEMSGICON.OLEMSGICON_CRITICAL,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }

    /// <summary>
    /// Shows a warning message to the user.
    /// </summary>
    /// <param name="message">Warning message.</param>
    private void ShowWarning(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        VsShellUtilities.ShowMessageBox(
            this.serviceProvider,
            message,
            "Settex Build Warning",
            OLEMSGICON.OLEMSGICON_WARNING,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}
