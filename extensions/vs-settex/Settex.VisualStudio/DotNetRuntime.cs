namespace Settex.VisualStudio;

using System;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// Detects whether the .NET runtime required by the Settex language server (the
/// .NET 10 shared framework) is installed. Lets the extension show an actionable
/// message instead of failing opaquely when the runtime is missing.
/// </summary>
internal static class DotNetRuntime
{
    /// <summary>
    /// The .NET download page shown to the user when the required runtime is missing.
    /// </summary>
    public const string DownloadUrl = "https://dotnet.microsoft.com/download/dotnet/10.0";

    /// <summary>
    /// The shared-framework line prefix that indicates a .NET 10 runtime. The
    /// trailing dot avoids matching e.g. "Microsoft.NETCore.App 100.x".
    /// </summary>
    private const string RequiredRuntimePrefix = "Microsoft.NETCore.App 10.";

    /// <summary>
    /// Returns whether a Microsoft.NETCore.App 10.x runtime is available, by
    /// invoking <c>dotnet --list-runtimes</c>. On failure (the <c>dotnet</c> CLI is
    /// not on PATH, the process cannot be started, etc.) <paramref name="detail"/>
    /// carries a human-readable reason.
    /// </summary>
    public static bool IsAvailable(out string detail)
    {
        try
        {
            var startInfo = new ProcessStartInfo("dotnet", "--list-runtimes")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);

            if (process == null)
            {
                detail = "The .NET CLI ('dotnet') could not be started.";
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (HasRequiredRuntime(output))
            {
                detail = string.Empty;
                return true;
            }

            detail = "The .NET 10 runtime was not found ('dotnet --list-runtimes' listed no Microsoft.NETCore.App 10.x).";
            return false;
        }
        catch (Exception ex)
        {
            // Most commonly: 'dotnet' is not installed or not on PATH.
            detail = $"The .NET CLI ('dotnet') was not found on your PATH ({ex.Message}).";
            return false;
        }
    }

    /// <summary>
    /// Parses the output of <c>dotnet --list-runtimes</c> and returns whether it
    /// lists a Microsoft.NETCore.App 10.x runtime. Each relevant line looks like:
    /// <c>Microsoft.NETCore.App 10.0.9 [C:\Program Files\dotnet\shared\...]</c>.
    /// </summary>
    public static bool HasRequiredRuntime(string listRuntimesOutput)
    {
        if (string.IsNullOrEmpty(listRuntimesOutput))
        {
            return false;
        }

        return listRuntimesOutput
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Any(line => line.StartsWith(RequiredRuntimePrefix, StringComparison.Ordinal));
    }
}
