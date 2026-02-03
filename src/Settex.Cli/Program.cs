namespace Settex.Cli;

using System.CommandLine;

using Settex.Compilation;

using Spectre.Console;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Settex CLI - Compile Settex configuration files to JSON");

        var buildCommand = new Command("build", "Compile a .settex file to JSON files");

        var fileArgument = new Argument<FileInfo>(
            "file",
            "Path to the .settex file to compile")
        {
            Arity = ArgumentArity.ExactlyOne,
        };

        buildCommand.AddArgument(fileArgument);

        var outputOption = new Option<DirectoryInfo?>(
            ["-o", "--output"],
            description: "Output directory (default: current directory)",
            getDefaultValue: () => new("."));

        buildCommand.AddOption(outputOption);

        buildCommand.SetHandler(ExecuteBuildAsync, fileArgument, outputOption);

        rootCommand.AddCommand(buildCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> ExecuteBuildAsync(FileInfo sourceFile, DirectoryInfo? outputDirectory)
    {
        return await Task.Run(() => ExecuteBuild(sourceFile, outputDirectory ?? new DirectoryInfo(".")));
    }

    private static int ExecuteBuild(FileInfo sourceFile, DirectoryInfo outputDirectory)
    {
        if (!sourceFile.Exists)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] File not found: {0}", sourceFile.FullName);
            return 1;
        }

        if (!outputDirectory.Exists)
        {
            try
            {
                outputDirectory.Create();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Could not create output directory: {0}", ex.Message);
                return 1;
            }
        }

        AnsiConsole.Status()
                   .Spinner(Spinner.Known.Dots)
                   .Start($"Compiling [cyan]{sourceFile.Name}[/]...",
                        ctx =>
                        {
                            // Give a brief visual feedback
                            Thread.Sleep(100);
                        });

        var compiler = new SettexCompiler();
        var result = compiler.Compile(sourceFile.FullName, outputDirectory.FullName);

        if (result.Diagnostics.Count > 0)
        {
            AnsiConsole.WriteLine();
            PrintDiagnostics(result.Diagnostics, sourceFile.FullName);
        }

        if (!result.Success)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]✗ Compilation failed.[/]");
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Successfully generated {0} file(s):[/]", result.GeneratedFiles.Count);

        var table = new Table()
                   .Border(TableBorder.None)
                   .HideHeaders()
                   .AddColumn("");

        foreach (var file in result.GeneratedFiles)
        {
            table.AddRow($"  [dim]→[/] [cyan]{Path.GetFileName(file)}[/]");
        }

        AnsiConsole.Write(table);

        return 0;
    }

    private static void PrintDiagnostics(IReadOnlyList<Diagnostic> diagnostics, string sourceFile)
    {
        var table = new Table()
                   .Border(TableBorder.Rounded)
                   .BorderColor(Color.Grey);

        table.AddColumn(new TableColumn("Severity").Centered());
        table.AddColumn(new TableColumn("Location").LeftAligned());
        table.AddColumn(new TableColumn("Message").LeftAligned());

        foreach (var diagnostic in diagnostics)
        {
            var severityMarkup = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => "[red]Error[/]",
                DiagnosticSeverity.Warning => "[yellow]Warning[/]",
                DiagnosticSeverity.Info => "[blue]Info[/]",
                _ => "[dim]Note[/]",
            };

            var location = diagnostic.Location;
            var locationText = location != null
                                       ? $"[dim]{Path.GetFileName(sourceFile)}({location.Line},{location.Column})[/]"
                                       : $"[dim]{Path.GetFileName(sourceFile)}[/]";

            table.AddRow(severityMarkup, locationText, diagnostic.Message.EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }
}
