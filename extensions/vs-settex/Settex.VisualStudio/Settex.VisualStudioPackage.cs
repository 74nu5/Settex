namespace Settex.VisualStudio;

using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// This is the class that implements the package exposed by this assembly.
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageGuidString)]
[ProvideOptionPage(typeof(SettexOptionsPage), "Settex", "General", 0, 0, true)]
public sealed class SettexVisualStudioPackage : AsyncPackage
{
    /// <summary>
    /// Package GUID string.
    /// </summary>
    public const string PackageGuidString = "CF2F7AA1-CFD1-4FBD-9A5E-6BA5B3FE5ED8";

    private SettexDocumentEventHandler documentEventHandler = null!;

    /// <summary>
    /// Initialization of the package; this method is called right after the package is sited.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <returns>A task representing the async work of package initialization.</returns>
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await base.InitializeAsync(cancellationToken, progress);

        // When initialized asynchronously, switch to the main thread before accessing VS services
        await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        // Initialize the compile command
        await CompileSettexCommand.InitializeAsync(this);

        // Initialize document event handler for auto-compile on save
        await this.InitializeDocumentEventHandlerAsync();

        // Language client will be automatically initialized via MEF (SettexLanguageClient.cs)
        // TextMate grammar and syntax highlighting are configured via Settex.pkgdef
        // Code snippets are registered via pkgdef and located in the Snippets folder
    }

    /// <summary>
    /// Initializes the document event handler for auto-compile.
    /// </summary>
    /// <returns>Task.</returns>
    private async Task InitializeDocumentEventHandlerAsync()
    {
        await this.JoinableTaskFactory.SwitchToMainThreadAsync();

        // Get or create the Settex output pane
        var outputWindow = await this.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
        if (outputWindow == null)
        {
            return;
        }

        var paneGuid = new Guid("8E1C8B95-8F5D-4C3E-B5A1-8D5E6F7A8B9D"); // Unique GUID for Settex pane
        outputWindow.GetPane(ref paneGuid, out var pane);

        if (pane == null)
        {
            outputWindow.CreatePane(ref paneGuid, "Settex", 1, 1);
            outputWindow.GetPane(ref paneGuid, out pane);
        }

        if (pane == null)
        {
            return;
        }

        // Create build service and event handler
        var buildService = new SettexBuildService(this);
        this.documentEventHandler = new SettexDocumentEventHandler(this, buildService, pane);
        await this.documentEventHandler.SubscribeAsync();
    }

    /// <summary>
    /// Disposes the package.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (this.documentEventHandler != null)
            {
                if (ThreadHelper.CheckAccess())
                {
                    this.documentEventHandler.Unsubscribe();
                }
                else
                {
                    this.JoinableTaskFactory.Run(
                        async delegate
                        {
                            await this.JoinableTaskFactory.SwitchToMainThreadAsync();
                            this.documentEventHandler.Unsubscribe();
                        });
                }
            }
        }

        base.Dispose(disposing);
    }
}
