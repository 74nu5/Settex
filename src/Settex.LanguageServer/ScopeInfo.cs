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

        // Block-bearing nodes now carry a real end (the closing brace), so a scope
        // covers its whole body — including blank lines and the closing brace. Falls
        // back to the start line for nodes without an end span.
        this.EndLine = location.EffectiveEndLine;
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
    /// Dernière ligne (1-based) couverte par ce scope. Calculée par le
    /// <see cref="ScopeResolver"/> à partir de l'étendue réelle du contenu du
    /// scope (statements, expressions, scopes enfants) plutôt que d'une heuristique.
    /// Le scope global couvre tout le fichier (<see cref="int.MaxValue"/>).
    /// </summary>
    public int EndLine { get; private set; }

    /// <summary>
    /// Étend la fin du scope pour inclure la ligne donnée (jamais rétrécie).
    /// </summary>
    internal void ExtendTo(int line)
    {
        if (line > this.EndLine)
        {
            this.EndLine = line;
        }
    }

    /// <summary>
    /// Vérifie si une position (1-based) est dans l'étendue de ce scope.
    /// </summary>
    public bool ContainsPosition(int line, int column)
    {
        var startLine = this.Location.Line;
        var startColumn = this.Location.Column;

        // Avant le début du scope ?
        if (line < startLine || (line == startLine && column < startColumn))
        {
            return false;
        }

        // Après la dernière ligne de contenu du scope ?
        return line <= this.EndLine;
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
