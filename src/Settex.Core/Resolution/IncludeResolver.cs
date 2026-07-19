namespace Settex.Core.Resolution;

using Settex.Core.Diagnostics;
using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;

/// <summary>
///     Resolves include statements and detects circular dependencies.
/// </summary>
public class IncludeResolver
{
    /// <summary>
    ///     Maximum include-nesting depth. Beyond this the resolver bails out with a
    ///     located diagnostic instead of risking an unrecoverable
    ///     <see cref="StackOverflowException" /> on a pathological (but non-circular)
    ///     include chain.
    /// </summary>
    private const int MaxIncludeDepth = 64;

    /// <summary>
    ///     Compares include paths using the host filesystem's case semantics:
    ///     case-insensitive on Windows, case-sensitive elsewhere. Prevents both a
    ///     missed cycle and a missed diamond when the same file is referenced with
    ///     different casing on a case-insensitive filesystem (e.g. Inc.settex vs
    ///     inc.settex on Windows).
    /// </summary>
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // Files already merged into the output, so a diamond include (A->B, A->C,
    // B->D, C->D) applies D exactly once instead of twice.
    private readonly HashSet<string> included = new(PathComparer);

    // The chain of files currently being resolved, used to detect cycles.
    private readonly Stack<string> includeStack = new();

    // Optional source of file contents, used by the language server to resolve
    // includes against open (possibly unsaved) editor buffers instead of disk.
    private readonly Func<string, string?>? contentProvider;

    /// <summary>
    ///     Creates a resolver that reads included files from disk.
    /// </summary>
    public IncludeResolver()
        : this(null)
    {
    }

    /// <summary>
    ///     Creates a resolver that first asks <paramref name="contentProvider" /> for a
    ///     file's content (returning <c>null</c> to fall back to disk). This lets an
    ///     editor resolve includes against unsaved buffers.
    /// </summary>
    public IncludeResolver(Func<string, string?>? contentProvider)
        => this.contentProvider = contentProvider;

    /// <summary>
    ///     Every file touched during resolution: the root, all transitively included
    ///     files, and any file an <c>include</c> pointed at but that could not be found.
    ///     Lets a host track which documents depend on which files — including the
    ///     missing one, whose later creation is exactly what should clear the error.
    /// </summary>
    public IReadOnlyCollection<string> ResolvedFiles => this.included;

    /// <summary>
    ///     Resolves an include path relative to the current file.
    /// </summary>
    /// <param name="includePath">The path specified in the include statement (relative or absolute)</param>
    /// <param name="currentFilePath">The path of the file containing the include statement</param>
    /// <returns>Absolute path to the included file</returns>
    public string ResolveIncludePath(string includePath, string currentFilePath)
    {
        var currentDirectory = Path.GetDirectoryName(currentFilePath) ?? Directory.GetCurrentDirectory();
        var absolutePath = Path.GetFullPath(Path.Combine(currentDirectory, includePath));
        return absolutePath;
    }

    /// <summary>
    ///     Loads and parses a file, returning its AST.
    /// </summary>
    /// <param name="filePath">Absolute path to the file</param>
    /// <returns>Parsed FileNode AST</returns>
    /// <exception cref="IncludeException">If file cannot be read or parsed</exception>
    public FileNode LoadAndParseFile(string filePath, SourceLocation? location = null)
    {
        // An open editor buffer wins over the on-disk copy, so analysis reflects
        // unsaved edits in included files.
        var providedSource = this.contentProvider?.Invoke(filePath);

        if (providedSource is null && !File.Exists(filePath))
        {
            throw new IncludeException($"Include file not found: {filePath}", location);
        }

        try
        {
            var source = providedSource ?? File.ReadAllText(filePath);
            var lexer = new Lexer(source, filePath);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens, filePath);
            return parser.Parse();
        }
        catch (Exception ex) when (ex is not IncludeException)
        {
            throw new IncludeException($"Failed to parse include file '{filePath}': {ex.Message}", location, ex);
        }
    }

    /// <summary>
    ///     Detects if including a file would create a cycle.
    /// </summary>
    /// <param name="filePath">The file to check</param>
    /// <returns>True if a cycle would be created</returns>
    public bool DetectCycle(string filePath)
        => this.includeStack.Any(path => PathComparer.Equals(path, filePath));

    /// <summary>
    ///     Resolves all includes in a file recursively, returning a flattened list of top-level statements.
    /// </summary>
    /// <param name="fileNode">The parsed AST of the main file</param>
    /// <param name="filePath">The path of the main file</param>
    /// <returns>Flattened list of top-level statements with all includes resolved</returns>
    public List<ITopLevelStatement> ResolveIncludes(FileNode fileNode, string filePath)
    {
        this.includeStack.Push(filePath);
        this.included.Add(filePath);

        var result = new List<ITopLevelStatement>();

        foreach (var statement in fileNode.Statements)
        {
            if (statement is IncludeNode includeNode)
            {
                var includePath = this.ResolveIncludePath(includeNode.Path, filePath);

                if (this.DetectCycle(includePath))
                {
                    var cycle = string.Join(" -> ", this.includeStack.Reverse()) + $" -> {includePath}";
                    throw new IncludeException($"Circular include detected: {cycle}", includeNode.Location);
                }

                // Diamond dedup: a file already merged in via another branch is
                // included exactly once (cycle is checked first, above, so an
                // ancestor still on the stack reports a cycle rather than a skip).
                if (this.included.Contains(includePath))
                {
                    continue;
                }

                if (this.includeStack.Count >= MaxIncludeDepth)
                {
                    throw new IncludeException(
                        $"Include depth limit ({MaxIncludeDepth}) exceeded while including '{includePath}'; the include chain is too deeply nested.",
                        includeNode.Location);
                }

                // Record the file before trying to load it. When it does not exist this
                // is the only point at which its path is known — and it is precisely the
                // file whose creation should re-analyse this document, so a host that
                // tracked only successfully resolved files could never clear the
                // "include not found" error without an unrelated edit.
                this.included.Add(includePath);

                var includedFile = this.LoadAndParseFile(includePath, includeNode.Location);
                var includedStatements = this.ResolveIncludes(includedFile, includePath);
                result.AddRange(includedStatements);
            }
            else
            {
                result.Add(statement);
            }
        }

        this.includeStack.Pop();
        return result;
    }
}
