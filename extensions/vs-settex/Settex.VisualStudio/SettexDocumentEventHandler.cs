namespace Settex.VisualStudio;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// Handles document save events to automatically compile .settex files.
/// </summary>
internal class SettexDocumentEventHandler : IVsRunningDocTableEvents
{
    private readonly AsyncPackage package;
    private readonly ISettexBuildService buildService;
    private readonly IVsOutputWindowPane outputPane;
    private RunningDocumentTable runningDocumentTable = null!;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> activeCompilations = new ConcurrentDictionary<string, CancellationTokenSource>();

    /// <summary>
    /// Initializes a new instance of the <see cref="SettexDocumentEventHandler"/> class.
    /// </summary>
    /// <param name="package">Owner package.</param>
    /// <param name="buildService">Build service.</param>
    /// <param name="outputPane">Output window pane for logging.</param>
    public SettexDocumentEventHandler(AsyncPackage package, ISettexBuildService buildService, IVsOutputWindowPane outputPane)
    {
        this.package = package ?? throw new ArgumentNullException(nameof(package));
        this.buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
        this.outputPane = outputPane ?? throw new ArgumentNullException(nameof(outputPane));
    }

    /// <summary>
    /// Subscribes to document save events.
    /// </summary>
    /// <returns>Task.</returns>
    public async Task SubscribeAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        
        this.runningDocumentTable = new RunningDocumentTable(this.package);
        this.runningDocumentTable.Advise(this);
    }

    /// <summary>
    /// Unsubscribes from document save events.
    /// </summary>
    public void Unsubscribe()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        
        if (this.runningDocumentTable != null)
        {
            this.runningDocumentTable.Dispose();
            this.runningDocumentTable = null!;
        }

        // Cancel and dispose all active compilations
        foreach (var kvp in this.activeCompilations)
        {
            kvp.Value.Cancel();
            // Unconditionally remove all entries during cleanup
            if (this.activeCompilations.TryRemove(kvp.Key, out var removedCts))
            {
                removedCts.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
    {
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
    {
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int OnAfterSave(uint docCookie)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        // Get options and capture values on UI thread to avoid cross-thread access
        var optionsPage = this.package.GetDialogPage(typeof(SettexOptionsPage)) as SettexOptionsPage;
        if (optionsPage == null || !optionsPage.CompileOnSave)
        {
            return VSConstants.S_OK;
        }

        // Capture option values on UI thread before async work
        var showSuccessNotifications = optionsPage.ShowSuccessNotifications;
        var showErrorNotifications = optionsPage.ShowErrorNotifications;
        var logToOutputWindow = optionsPage.LogToOutputWindow;

        // Get document info
        var documentInfo = this.runningDocumentTable.GetDocumentInfo(docCookie);
        var filePath = documentInfo.Moniker;

        // Check if it's a .settex file
        if (string.IsNullOrEmpty(filePath) || !filePath.EndsWith(".settex", StringComparison.OrdinalIgnoreCase))
        {
            return VSConstants.S_OK;
        }

        // Cancel any in-flight compilation for this file and start a new one
        var newCts = new CancellationTokenSource();
        this.activeCompilations.AddOrUpdate(
            filePath,
            newCts,
            (key, existingCts) =>
            {
                // Cancel the existing compilation
                existingCts.Cancel();
                
                // Schedule disposal of the old token after a delay to ensure its finally block completes
                // This prevents resource leaks when a compilation is replaced mid-flight
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(100);
                    existingCts.Dispose();
                });
                
                return newCts;
            });

        // Compile the file asynchronously
        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            try
            {
                await this.CompileFileAsync(filePath, showSuccessNotifications, showErrorNotifications, logToOutputWindow, newCts.Token);
            }
            finally
            {
                // Clean up the cancellation token source only if this is still the active compilation
                // If a new compilation started before this finally block, TryRemove will fail because
                // the dictionary contains the newer CTS, correctly preventing disposal of the wrong token
                if (this.activeCompilations.TryRemove(new KeyValuePair<string, CancellationTokenSource>(filePath, newCts)))
                {
                    newCts.Dispose();
                }
            }
        }).FileAndForget("settex/auto-compile");

        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
    {
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
    {
        return VSConstants.S_OK;
    }

    /// <inheritdoc/>
    public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
    {
        return VSConstants.S_OK;
    }

    /// <summary>
    /// Compiles a Settex file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the .settex file.</param>
    /// <param name="showSuccessNotifications">Whether to show success notifications.</param>
    /// <param name="showErrorNotifications">Whether to show error notifications.</param>
    /// <param name="logToOutputWindow">Whether to log to output window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task.</returns>
    private async Task CompileFileAsync(string filePath, bool showSuccessNotifications, bool showErrorNotifications, bool logToOutputWindow, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);

            if (logToOutputWindow)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                this.outputPane.OutputString($"[Settex] Compiling {fileName}...\n");
            }

            var result = await this.buildService.CompileSettexFileAsync(filePath, cancellationToken, showDialogs: false);

            if (result)
            {
                if (logToOutputWindow)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    this.outputPane.OutputString($"[Settex] Successfully compiled {fileName}\n");
                }

                if (showSuccessNotifications)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    VsShellUtilities.ShowMessageBox(
                        this.package,
                        $"Successfully compiled {fileName}",
                        "Settex Auto-Compile",
                        OLEMSGICON.OLEMSGICON_INFO,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
            else
            {
                if (logToOutputWindow)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    this.outputPane.OutputString($"[Settex] Failed to compile {fileName}\n");
                }

                // Show error notification if enabled
                if (showErrorNotifications)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    VsShellUtilities.ShowMessageBox(
                        this.package,
                        $"Failed to compile {fileName}. Check the Settex output pane for details.",
                        "Settex Auto-Compile Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Compilation was cancelled, this is expected
            if (logToOutputWindow)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                this.outputPane.OutputString($"[Settex] Compilation of {Path.GetFileName(filePath)} was cancelled\n");
            }
        }
        catch (Exception ex)
        {
            if (logToOutputWindow)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                this.outputPane.OutputString($"[Settex] Error: {ex.Message}\n");
            }

            if (showErrorNotifications)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    $"Error compiling {Path.GetFileName(filePath)}: {ex.Message}",
                    "Settex Auto-Compile Error",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}
