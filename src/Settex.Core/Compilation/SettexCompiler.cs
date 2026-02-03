namespace Settex.Compilation;

using Settex.Core.Diagnostics;
using Settex.Core.Evaluation;
using Settex.Core.Lexer;
using Settex.Core.Parser;
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
    /// <returns>Compilation result with diagnostics.</returns>
    public CompilationResult Compile(string sourceFilePath, string outputDirectory)
    {
        var diagnostics = new List<Diagnostic>();

        try
        {
            // Validate input
            if (!File.Exists(sourceFilePath))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    $"Source file not found: {sourceFilePath}"));
                return new CompilationResult(false, diagnostics);
            }

            // Read source
            string source;
            try
            {
                source = File.ReadAllText(sourceFilePath);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    $"Failed to read source file: {ex.Message}"));
                return new CompilationResult(false, diagnostics);
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
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    ex.Message,
                    ex.Location));
                return new CompilationResult(false, diagnostics);
            }

            // Phase 2: Parsing
            Core.Parser.Ast.FileNode ast;
            try
            {
                var parser = new Parser(tokens, sourceFilePath);
                ast = parser.Parse();
            }
            catch (ParserException ex)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    ex.Message,
                    ex.Location));
                return new CompilationResult(false, diagnostics);
            }

            // Phase 2.5: Resolve includes
            List<Core.Parser.Ast.ITopLevelStatement> resolvedStatements;
            try
            {
                var includeResolver = new IncludeResolver();
                resolvedStatements = includeResolver.ResolveIncludes(ast, sourceFilePath);
            }
            catch (IncludeException ex)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    ex.Message,
                    ex.Location));
                return new CompilationResult(false, diagnostics);
            }

            // Rebuild AST with resolved includes
            ast = new Core.Parser.Ast.FileNode(resolvedStatements, ast.Location);

            // Phase 3: Evaluation
            SettingsModel model;
            try
            {
                var evaluator = new Evaluator();
                model = evaluator.Evaluate(ast);
            }
            catch (EvaluatorException ex)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    ex.Message,
                    ex.Location));
                return new CompilationResult(false, diagnostics);
            }

            // Phase 4: Writing (includes merging via JsonWriter)
            List<string> generatedFiles;
            try
            {
                var writer = new JsonWriter();
                generatedFiles = writer.WriteSettings(model, outputDirectory);
            }
            catch (JsonWriterException ex)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    ex.Message,
                    ex.Location));
                return new CompilationResult(false, diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    $"Failed to write output files: {ex.Message}"));
                return new CompilationResult(false, diagnostics);
            }

            // Success
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Info,
                $"Successfully compiled {sourceFilePath}"));

            return new CompilationResult(true, diagnostics, generatedFiles);
        }
        catch (Exception ex)
        {
            // Catch-all for unexpected errors
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                $"Unexpected error during compilation: {ex.Message}"));
            return new CompilationResult(false, diagnostics);
        }
    }
}
