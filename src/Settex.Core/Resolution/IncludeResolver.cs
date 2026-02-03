namespace Settex.Core.Resolution;

using Settex.Core.Lexer;
using Settex.Core.Parser;
using Settex.Core.Parser.Ast;

/// <summary>
///     Resolves include statements and detects circular dependencies.
/// </summary>
public class IncludeResolver
{
    private readonly HashSet<string> visited = [];
    private readonly Stack<string> includeStack = [];

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
    public FileNode LoadAndParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new IncludeException($"Include file not found: {filePath}", null);
        }

        try
        {
            var source = File.ReadAllText(filePath);
            var lexer = new Lexer(source, filePath);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens, filePath);
            return parser.Parse();
        }
        catch (Exception ex) when (ex is not IncludeException)
        {
            throw new IncludeException($"Failed to parse include file '{filePath}': {ex.Message}", null, ex);
        }
    }

    /// <summary>
    ///     Detects if including a file would create a cycle.
    /// </summary>
    /// <param name="filePath">The file to check</param>
    /// <returns>True if a cycle would be created</returns>
    public bool DetectCycle(string filePath)
        => this.includeStack.Contains(filePath);

    /// <summary>
    ///     Resolves all includes in a file recursively, returning a flattened list of top-level statements.
    /// </summary>
    /// <param name="fileNode">The parsed AST of the main file</param>
    /// <param name="filePath">The path of the main file</param>
    /// <returns>Flattened list of top-level statements with all includes resolved</returns>
    public List<ITopLevelStatement> ResolveIncludes(FileNode fileNode, string filePath)
    {
        this.includeStack.Push(filePath);
        this.visited.Add(filePath);

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

                var includedFile = this.LoadAndParseFile(includePath);
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
