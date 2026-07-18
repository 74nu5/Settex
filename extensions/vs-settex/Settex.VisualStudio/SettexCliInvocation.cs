namespace Settex.VisualStudio;

/// <summary>
/// Builds the command-line invocation for the Settex CLI.
/// Kept free of any Visual Studio SDK dependency so it can be unit tested
/// on modern .NET even though the extension itself targets .NET Framework.
/// </summary>
internal static class SettexCliInvocation
{
    /// <summary>
    /// The Settex CLI verb used to compile a .settex file to appsettings*.json.
    /// The CLI (see src/Settex.Cli/Program.cs) only exposes the <c>build</c> command.
    /// </summary>
    public const string CompileCommand = "build";

    /// <summary>
    /// Builds the argument string passed to <c>dotnet</c> to compile a single file.
    /// </summary>
    /// <param name="cliPath">Path to the Settex.Cli.dll.</param>
    /// <param name="settexFilePath">Path to the .settex file to compile.</param>
    /// <returns>The fully-quoted argument string for the dotnet process.</returns>
    public static string BuildCompileArguments(string cliPath, string settexFilePath)
    {
        return $"\"{cliPath}\" {CompileCommand} \"{settexFilePath}\"";
    }
}
