namespace Settex.VisualStudio.Tests;

using Settex.VisualStudio;

using TUnit.Core;

/// <summary>
/// Tests for the .NET runtime detection used by the Visual Studio extension's
/// language client to show an actionable message when the .NET 10 runtime the
/// server needs is missing.
/// </summary>
public class DotNetRuntimeTests
{
    private const string WithNet10 =
        "Microsoft.AspNetCore.App 8.0.11 [C:\\Program Files\\dotnet\\shared\\Microsoft.AspNetCore.App]\r\n" +
        "Microsoft.NETCore.App 8.0.11 [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]\r\n" +
        "Microsoft.NETCore.App 10.0.9 [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]\r\n";

    private const string WithoutNet10 =
        "Microsoft.AspNetCore.App 8.0.11 [C:\\Program Files\\dotnet\\shared\\Microsoft.AspNetCore.App]\r\n" +
        "Microsoft.NETCore.App 8.0.11 [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]\r\n";

    [Test]
    public async Task HasRequiredRuntime_WithNet10Listed_ReturnsTrue()
    {
        await Assert.That(DotNetRuntime.HasRequiredRuntime(WithNet10)).IsTrue();
    }

    [Test]
    public async Task HasRequiredRuntime_WithoutNet10_ReturnsFalse()
    {
        await Assert.That(DotNetRuntime.HasRequiredRuntime(WithoutNet10)).IsFalse();
    }

    [Test]
    public async Task HasRequiredRuntime_DoesNotMatchNet100()
    {
        // "Microsoft.NETCore.App 100.x" must not be mistaken for a .NET 10 runtime.
        const string net100 = "Microsoft.NETCore.App 100.0.0 [C:\\dotnet\\shared\\Microsoft.NETCore.App]";
        await Assert.That(DotNetRuntime.HasRequiredRuntime(net100)).IsFalse();
    }

    [Test]
    public async Task HasRequiredRuntime_EmptyOrNull_ReturnsFalse()
    {
        await Assert.That(DotNetRuntime.HasRequiredRuntime(string.Empty)).IsFalse();
        await Assert.That(DotNetRuntime.HasRequiredRuntime(null!)).IsFalse();
    }

    [Test]
    public async Task HasRequiredRuntime_ToleratesWhitespaceAndLineEndings()
    {
        const string padded = "   Microsoft.NETCore.App 10.0.1 [/usr/share/dotnet/shared/Microsoft.NETCore.App]   \n";
        await Assert.That(DotNetRuntime.HasRequiredRuntime(padded)).IsTrue();
    }
}
