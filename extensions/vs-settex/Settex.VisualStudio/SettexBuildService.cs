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
    /// <returns>True if compilation succeeded, false otherwise.</returns>
    public async Task<bool> CompileSettexFileAsync(string settexFilePath, CancellationToken cancellationToken)
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
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                this.ShowWarning("Settex compiler not found. Please ensure Settex.Cli is installed.");
                return false;
            }

            // Get the directory of the settex file; fall back to current directory if none
            var directory = Path.GetDirectoryName(settexFilePath) ?? Directory.GetCurrentDirectory();

            // Run the Settex compiler
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{cliPath}\" compile \"{settexFilePath}\"",
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(processStartInfo))
            {
                if (process == null)
                {
                    return false;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                // WaitForExitAsync is not available on net472, use synchronous wait with Task.Run
                await Task.Run(() => process.WaitForExit(), cancellationToken);

                if (process.ExitCode != 0)
                {
                    Debug.WriteLine($"Settex compilation failed: {error}");
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    this.ShowError($"Settex compilation failed:\n{error}");
                    return false;
                }

                Debug.WriteLine($"Settex compilation succeeded: {output}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error compiling Settex file: {ex.Message}");
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            this.ShowError($"Error compiling Settex file: {ex.Message}");
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
        var searchPaths = new[]
        {
            // Development location
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..", "src", "Settex.Cli", "bin", "Debug", "net10.0", "Settex.Cli.dll")),
            
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
