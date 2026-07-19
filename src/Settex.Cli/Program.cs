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

        var mergedOption = new Option<bool>(
            ["--merged"],
            description: "Write the full merged config in each environment file instead of only its overrides");

        buildCommand.AddOption(mergedOption);

        var noCoverageOption = new Option<bool>(
            ["--no-coverage-check"],
            description: "Disable the cross-environment coverage check (keys set in some environments but not others)");

        buildCommand.AddOption(noCoverageOption);

        var noArrayLayeringOption = new Option<bool>(
            ["--no-array-layering-check"],
            description: "Disable the array-layering check (an environment override that would keep base elements at runtime)");

        buildCommand.AddOption(noArrayLayeringOption);

        buildCommand.SetHandler(ExecuteBuildAsync, fileArgument, outputOption, mergedOption, noCoverageOption, noArrayLayeringOption);

        rootCommand.AddCommand(buildCommand);

        var importCommand = new Command(
            "import",
            "Turn an existing appsettings.json family into a .settex file, verified equivalent");

        var importBaseArgument = new Argument<FileInfo>(
            "file",
            "Path to the base appsettings.json; sibling appsettings.{Environment}.json files are picked up automatically")
        {
            Arity = ArgumentArity.ExactlyOne,
        };

        importCommand.AddArgument(importBaseArgument);

        var importOutputOption = new Option<FileInfo?>(
            ["-o", "--output"],
            description: "Output .settex file (default: appsettings.settex next to the base file)");

        importCommand.AddOption(importOutputOption);

        importCommand.SetHandler(ExecuteImportAsync, importBaseArgument, importOutputOption);

        rootCommand.AddCommand(importCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> ExecuteImportAsync(FileInfo baseFile, FileInfo? output)
        => await Task.Run(() => ExecuteImport(baseFile, output));

    private static int ExecuteImport(FileInfo baseFile, FileInfo? output)
    {
        if (!baseFile.Exists)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] File not found: {0}", baseFile.FullName);
            return 1;
        }

        try
        {
            var directory = baseFile.DirectoryName ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(baseFile.Name);

            var baseSettings = ReadJsonObject(baseFile.FullName);

            // Sibling appsettings.{Environment}.json files become env blocks, named
            // after the segment between the base name and the extension.
            var environments = new Dictionary<string, System.Text.Json.Nodes.JsonObject>();

            foreach (var sibling in Directory.GetFiles(directory, baseName + ".*.json"))
            {
                var envName = Path.GetFileNameWithoutExtension(sibling)[(baseName.Length + 1)..];
                environments[envName] = ReadJsonObject(sibling);
                AnsiConsole.MarkupLine("  [dim]+[/] environment [cyan]{0}[/] from {1}", envName, Path.GetFileName(sibling));
            }

            var settex = Core.Importing.JsonImporter.GenerateSettex(baseSettings, environments);

            // The point of importing over rewriting: the result is proven equivalent
            // before anyone commits to it. A migration that is probably right is worse
            // than none — a missed key surfaces at runtime, in the environment where
            // it was missed.
            var differences = Core.Importing.JsonImporter.VerifyRoundTrip(settex, baseSettings, environments);

            if (differences.Count > 0)
            {
                AnsiConsole.MarkupLine("[red]✗ Import verification failed; no file was written.[/]");

                foreach (var difference in differences.Take(10))
                {
                    AnsiConsole.MarkupLine("  [red]-[/] {0}", Markup.Escape(difference));
                }

                return 1;
            }

            var outputPath = output?.FullName ?? Path.Combine(directory, baseName + ".settex");
            File.WriteAllText(outputPath, settex);

            AnsiConsole.MarkupLine(
                "[green]✓ Imported {0} environment(s); round-trip verified exact.[/]",
                environments.Count);
            AnsiConsole.MarkupLine("  [dim]→[/] [cyan]{0}[/]", outputPath);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] {0}", Markup.Escape(ex.Message));
            return 1;
        }
    }

    private static System.Text.Json.Nodes.JsonObject ReadJsonObject(string path)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(
            File.ReadAllText(path),
            documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });

        return node as System.Text.Json.Nodes.JsonObject
            ?? throw new InvalidOperationException($"{path} does not contain a JSON object.");
    }

    private static async Task<int> ExecuteBuildAsync(
        FileInfo sourceFile,
        DirectoryInfo? outputDirectory,
        bool merged,
        bool noCoverageCheck,
        bool noArrayLayeringCheck)
    {
        var options = new CompilerOptions
        {
            MergeEnvironments = merged,
            CheckCoverage = !noCoverageCheck,
            CheckArrayLayering = !noArrayLayeringCheck,
        };

        return await Task.Run(() => ExecuteBuild(sourceFile, outputDirectory ?? new DirectoryInfo("."), options));
    }

    private static int ExecuteBuild(FileInfo sourceFile, DirectoryInfo outputDirectory, CompilerOptions options)
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
        var result = compiler.Compile(sourceFile.FullName, outputDirectory.FullName, options);

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
