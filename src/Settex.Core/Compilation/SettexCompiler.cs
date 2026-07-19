namespace Settex.Compilation;

using Settex.Core.Evaluation;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;
using Settex.Core.Resolution;
using Settex.Core.Writing;

/// <summary>
///     Main compiler for Settex files.
///     Orchestrates lexing, parsing, evaluation, and JSON writing.
/// </summary>
public sealed class SettexCompiler
{
    /// <summary>
    ///     Compiles a Settex source file to appsettings*.json files.
    /// </summary>
    /// <param name="sourceFilePath">Path to the .settex source file.</param>
    /// <param name="outputDirectory">Directory where JSON files will be written.</param>
    /// <param name="options">Compilation options; defaults to <see cref="CompilerOptions.Default"/>.</param>
    /// <returns>Compilation result with diagnostics.</returns>
    public CompilationResult Compile(string sourceFilePath, string outputDirectory, CompilerOptions? options = null)
    {
        options ??= CompilerOptions.Default;
        var diagnostics = new List<Diagnostic>();

        try
        {
            // Validate input
            if (!File.Exists(sourceFilePath))
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    $"Source file not found: {sourceFilePath}"));

                return new(false, diagnostics);
            }

            // Read source
            string source;

            try
            {
                source = File.ReadAllText(sourceFilePath);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    $"Failed to read source file: {ex.Message}"));

                return new(false, diagnostics);
            }

            // Phase 1: Lexing
            List<Token> tokens;

            try
            {
                var lexer = new Lexer(source, sourceFilePath);
                tokens = lexer.Tokenize();
            }
            catch (LexerException ex)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    ex.Message,
                    ex.Location));

                return new(false, diagnostics);
            }

            // Phase 2: Parsing
            FileNode ast;

            try
            {
                var parser = new Parser(tokens, sourceFilePath);
                ast = parser.Parse();
            }
            catch (ParserException ex)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    ex.Message,
                    ex.Location));

                return new(false, diagnostics);
            }

            // Phase 2.5: Resolve includes
            List<ITopLevelStatement> resolvedStatements;

            try
            {
                var includeResolver = new IncludeResolver();
                resolvedStatements = includeResolver.ResolveIncludes(ast, sourceFilePath);
            }
            catch (IncludeException ex)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    ex.Message,
                    ex.Location));

                return new(false, diagnostics);
            }

            // Rebuild AST with resolved includes
            ast = new(resolvedStatements, ast.Location);

            // Phase 3: Evaluation
            SettingsModel model;

            try
            {
                var evaluator = new Evaluator();
                model = evaluator.Evaluate(ast);
            }
            catch (EvaluatorException ex)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    ex.Message,
                    ex.Location));

                return new(false, diagnostics);
            }

            // Phase 3.5: Cross-environment coverage check (advisory warnings).
            if (options.CheckCoverage)
            {
                diagnostics.AddRange(CoverageAnalyzer.Analyze(model));
            }

            // Phase 4: Writing (includes merging via JsonWriter)
            List<string> generatedFiles;

            try
            {
                var writer = new JsonWriter();
                generatedFiles = writer.WriteSettings(model, outputDirectory, options.MergeEnvironments);
            }
            catch (JsonWriterException ex)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    ex.Message,
                    ex.Location));

                return new(false, diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    $"Failed to write output files: {ex.Message}"));

                return new(false, diagnostics);
            }

            // Success
            diagnostics.Add(new(
                DiagnosticSeverity.Info,
                $"Successfully compiled {sourceFilePath}"));

            return new(true, diagnostics, generatedFiles);
        }
        catch (Exception ex)
        {
            // Catch-all for unexpected errors
            diagnostics.Add(new(
                DiagnosticSeverity.Error,
                $"Unexpected error during compilation: {ex.Message}"));

            return new(false, diagnostics);
        }
    }
}
