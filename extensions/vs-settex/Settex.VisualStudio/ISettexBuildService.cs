namespace Settex.VisualStudio;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for the Settex build service.
/// </summary>
internal interface ISettexBuildService
{
    /// <summary>
    /// Compiles a Settex file to appsettings.json.
    /// </summary>
    /// <param name="settexFilePath">Path to the .settex file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="showDialogs">Whether to show error/warning dialogs.</param>
    /// <returns>True if compilation succeeded, false otherwise.</returns>
    Task<bool> CompileSettexFileAsync(string settexFilePath, CancellationToken cancellationToken, bool showDialogs = true);

    /// <summary>
    /// Compiles all Settex files in a project.
    /// </summary>
    /// <param name="projectPath">Path to the project directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all compilations succeeded, false otherwise.</returns>
    Task<bool> CompileProjectSettexFilesAsync(string projectPath, CancellationToken cancellationToken);
}
