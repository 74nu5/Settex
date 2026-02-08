namespace Settex.VisualStudio;

using System;
using System.IO;
using System.Threading;

using EnvDTE;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

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

        // Get options
        var optionsPage = this.package.GetDialogPage(typeof(SettexOptionsPage)) as SettexOptionsPage;
        if (optionsPage == null || !optionsPage.CompileOnSave)
        {
            return VSConstants.S_OK;
        }

        // Get document info
        var documentInfo = this.runningDocumentTable.GetDocumentInfo(docCookie);
        var filePath = documentInfo.Moniker;

        // Check if it's a .settex file
        if (string.IsNullOrEmpty(filePath) || !filePath.EndsWith(".settex", StringComparison.OrdinalIgnoreCase))
        {
            return VSConstants.S_OK;
        }

        // Compile the file asynchronously
        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            await this.CompileFileAsync(filePath, optionsPage);
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
    /// <param name="options">Extension options.</param>
    /// <returns>Task.</returns>
    private async Task CompileFileAsync(string filePath, SettexOptionsPage options)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);

            if (options.LogToOutputWindow)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                this.outputPane.OutputString($"[Settex] Compiling {fileName}...\n");
            }

            var result = await this.buildService.CompileSettexFileAsync(filePath, CancellationToken.None, showDialogs: false);

            if (result)
            {
                if (options.LogToOutputWindow)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    this.outputPane.OutputString($"[Settex] Successfully compiled {fileName}\n");
                }

                if (options.ShowSuccessNotifications)
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
                if (options.LogToOutputWindow)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    this.outputPane.OutputString($"[Settex] Failed to compile {fileName}\n");
                }

                // Error notification is handled by the build service
            }
        }
        catch (Exception ex)
        {
            if (options.LogToOutputWindow)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                this.outputPane.OutputString($"[Settex] Error: {ex.Message}\n");
            }
        }
    }
}
