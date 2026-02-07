namespace Settex.VisualStudio;

using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio.Shell;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// This is the class that implements the package exposed by this assembly.
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageGuidString)]
[ProvideLanguageService(typeof(SettexLanguageClient), "Settex", 0, ShowHotURLs = false, DefaultToNonHotURLs = true, EnableCommenting = true, MatchBraces = true, ShowMatchingBrace = true)]
[ProvideLanguageExtension(typeof(SettexLanguageClient), ".settex")]
public sealed class SettexVisualStudioPackage : AsyncPackage
{
    /// <summary>
    /// Package GUID string.
    /// </summary>
    public const string PackageGuidString = "CF2F7AA1-CFD1-4FBD-9A5E-6BA5B3FE5ED8";

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

        // Language client will be automatically initialized via MEF (SettexLanguageClient.cs)
        // TextMate grammar and syntax highlighting are configured via Settex.pkgdef
        // Code snippets are registered via pkgdef and located in the Snippets folder
    }
}
