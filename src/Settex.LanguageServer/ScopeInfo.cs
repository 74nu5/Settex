using Settex.Core.Diagnostics;
using Settex.Core.Parser.Ast;

namespace Settex.LanguageServer;

/// <summary>
/// Type de scope dans le code Settex.
/// </summary>
public enum ScopeType
{
    /// <summary>
    /// Scope global (top-level du fichier).
    /// </summary>
    Global,

    /// <summary>
    /// Scope d'un environnement (env "Name" { ... }).
    /// </summary>
    Env,

    /// <summary>
    /// Scope d'une boucle for.
    /// </summary>
    ForLoop
}

/// <summary>
/// Représente un scope lexical dans le code Settex.
/// Les scopes sont organisés hiérarchiquement (global → env → for).
/// </summary>
public class ScopeInfo
{
    public ScopeInfo(
        ScopeType type,
        SourceLocation location,
        ScopeInfo? parent = null,
        string? name = null)
    {
        this.Type = type;
        this.Location = location;
        this.Parent = parent;
        this.Name = name;
        this.Variables = new List<LetNode>();
        this.Children = new List<ScopeInfo>();
    }

    /// <summary>
    /// Type du scope.
    /// </summary>
    public ScopeType Type { get; }

    /// <summary>
    /// Nom du scope (nom de l'environnement ou de l'iterator).
    /// Null pour le scope global.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Scope parent (null pour le scope global).
    /// </summary>
    public ScopeInfo? Parent { get; }

    /// <summary>
    /// Variables déclarées dans ce scope (let statements).
    /// </summary>
    public List<LetNode> Variables { get; }

    /// <summary>
    /// Scopes enfants (env blocks, for loops).
    /// </summary>
    public List<ScopeInfo> Children { get; }

    /// <summary>
    /// Localisation du scope dans le code source.
    /// </summary>
    public SourceLocation Location { get; }

    /// <summary>
    /// Vérifie si une position est dans ce scope.
    /// </summary>
    public bool ContainsPosition(int line, int column)
    {
        // Note: SourceLocation utilise line/column 1-based
        var startLine = this.Location.Line;
        var startColumn = this.Location.Column;
        var endLine = startLine;
        var endColumn = startColumn + this.Location.Length;

        // Pour un scope multi-ligne, on doit calculer la fin différemment
        // Pour l'instant, on utilise une heuristique simple
        if (line < startLine || (line == startLine && column < startColumn))
        {
            return false;
        }

        // Pour un scope, on considère qu'il s'étend jusqu'à la fin de son dernier enfant
        // ou jusqu'à la fin de sa propre location si pas d'enfants
        if (this.Children.Count > 0)
        {
            var lastChild = this.Children[^1];
            return lastChild.ContainsPosition(line, column) ||
                   (line <= lastChild.Location.Line + 100); // Heuristique généreuse
        }

        // Heuristique simple : 1000 lignes max pour un scope
        return line <= startLine + 1000;
    }

    /// <summary>
    /// Cherche une variable dans ce scope et ses parents.
    /// </summary>
    public LetNode? FindVariable(string name)
    {
        // Chercher dans ce scope
        var variable = this.Variables.FirstOrDefault(v => v.Name == name);
        if (variable != null)
        {
            return variable;
        }

        // Remonter au scope parent
        return this.Parent?.FindVariable(name);
    }

    public override string ToString()
    {
        var typeName = this.Type switch
        {
            ScopeType.Global => "Global",
            ScopeType.Env => $"Env({this.Name})",
            ScopeType.ForLoop => $"For({this.Name})",
            _ => "Unknown"
        };
        return $"{typeName} @ {this.Location.Line}:{this.Location.Column} ({this.Variables.Count} vars, {this.Children.Count} children)";
    }
}
