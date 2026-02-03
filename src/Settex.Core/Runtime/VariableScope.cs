namespace Settex.Core.Runtime;

/// <summary>
///     Manages variable scopes with lexical scoping rules.
///     Supports global scope, environment scopes, and local scopes (for, if, etc.).
/// </summary>
public sealed class VariableScope
{
    private readonly Dictionary<string, RuntimeValue> variables = [];
    private readonly VariableScope? parent;

    public VariableScope(VariableScope? parent = null)
    {
        this.parent = parent;
    }

    /// <summary>
    ///     Defines a variable in the current scope.
    /// </summary>
    public void Define(string name, RuntimeValue value)
    {
        this.variables[name] = value;
    }

    /// <summary>
    ///     Looks up a variable in the current scope or parent scopes.
    /// </summary>
    /// <returns>The value if found, null otherwise.</returns>
    public RuntimeValue? Lookup(string name)
    {
        if (this.variables.TryGetValue(name, out var value))
        {
            return value;
        }

        return this.parent?.Lookup(name);
    }

    /// <summary>
    ///     Checks if a variable is defined in the current scope or parent scopes.
    /// </summary>
    public bool IsDefined(string name)
    {
        return this.Lookup(name) != null;
    }

    /// <summary>
    ///     Creates a child scope that inherits from this scope.
    /// </summary>
    public VariableScope CreateChild()
    {
        return new VariableScope(this);
    }

    /// <summary>
    ///     Gets all variables defined in this scope (not including parent scopes).
    /// </summary>
    public IReadOnlyDictionary<string, RuntimeValue> GetLocalVariables()
    {
        return this.variables;
    }
}
