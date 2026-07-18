namespace Settex.VisualStudio.Tests;

using Settex.VisualStudio;

using TUnit.Core;

/// <summary>
/// Regression tests for the CLI invocation used by the Visual Studio extension's
/// build service. Guards against calling a CLI verb the compiler does not expose
/// (the extension previously invoked "compile", but the CLI only defines "build",
/// which made manual compile and compile-on-save fail at runtime).
/// </summary>
public class SettexCliInvocationTests
{
    [Test]
    public async Task BuildCompileArguments_UsesBuildVerb_NotCompile()
    {
        var args = SettexCliInvocation.BuildCompileArguments(
            @"C:\tools\Settex.Cli.dll",
            @"C:\proj\appsettings.settex");

        await Assert.That(args).Contains(" build ");
        await Assert.That(args).DoesNotContain(" compile ");
    }

    [Test]
    public async Task BuildCompileArguments_QuotesBothPaths()
    {
        const string cliPath = @"C:\Program Files\Settex\Settex.Cli.dll";
        const string filePath = @"C:\my proj\appsettings.settex";

        var args = SettexCliInvocation.BuildCompileArguments(cliPath, filePath);

        await Assert.That(args).IsEqualTo($"\"{cliPath}\" build \"{filePath}\"");
    }
}
