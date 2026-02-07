namespace Settex.VisualStudio;

using System;
using System.ComponentModel.Design;
using System.Threading;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// Command handler for compiling the current Settex file.
/// </summary>
internal sealed class CompileSettexCommand
{
    /// <summary>
    /// Command ID.
    /// </summary>
    public const int CommandId = 0x0100;

    /// <summary>
    /// Command menu group (command set GUID).
    /// </summary>
    public static readonly Guid CommandSet = new Guid("8E1C8B95-8F5D-4C3E-B5A1-8D5E6F7A8B9C");

    private readonly AsyncPackage package;
    private readonly ISettexBuildService buildService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompileSettexCommand"/> class.
    /// </summary>
    /// <param name="package">Owner package.</param>
    /// <param name="commandService">Command service to add command to.</param>
    /// <param name="buildService">Build service.</param>
    private CompileSettexCommand(AsyncPackage package, OleMenuCommandService commandService, ISettexBuildService buildService)
    {
        this.package = package ?? throw new ArgumentNullException(nameof(package));
        this.buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));

        if (commandService != null)
        {
            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }
    }

    /// <summary>
    /// Gets the instance of the command.
    /// </summary>
    public static CompileSettexCommand Instance { get; private set; }

    /// <summary>
    /// Initializes the singleton instance of the command.
    /// </summary>
    /// <param name="package">Owner package.</param>
    /// <returns>Task.</returns>
    public static async Task InitializeAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        var buildService = new SettexBuildService(package);

        Instance = new CompileSettexCommand(package, commandService, buildService);
    }

    /// <summary>
    /// This function is the callback used to execute the command when the menu item is clicked.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event args.</param>
    private void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        // Get the active document
        var dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE.DTE;
        if (dte?.ActiveDocument == null)
        {
            return;
        }

        var filePath = dte.ActiveDocument.FullName;
        if (!filePath.EndsWith(".settex", StringComparison.OrdinalIgnoreCase))
        {
            VsShellUtilities.ShowMessageBox(
                this.package,
                "The current file is not a .settex file.",
                "Compile Settex",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        // Compile the file
        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            var result = await this.buildService.CompileSettexFileAsync(filePath, CancellationToken.None);
            
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            
            if (result)
            {
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    "Settex file compiled successfully.",
                    "Compile Settex",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }).FileAndForget("settex/compile");
    }
}
