namespace Settex.Core.Evaluation;

using System.Text.Json.Nodes;

using Settex.Core.Parser.Ast;
using Settex.Core.Runtime;

/// <summary>
///     Evaluates an AST and produces a SettingsModel.
///     Performs validation and converts AST nodes to JSON representation.
///     V2: Supports variables with scopes.
/// </summary>
public class Evaluator
{
    /// <summary>
    ///     Evaluates a FileNode and returns a SettingsModel.
    /// </summary>
    public SettingsModel Evaluate(FileNode fileNode)
    {
        // Validate structure
        this.ValidateStructure(fileNode);

        // Create global scope and evaluate global let statements
        var globalScope = new VariableScope();
        this.EvaluateLetStatements(fileNode.Statements, globalScope);

        // Find the settings block and env blocks
        var settingsBlock = fileNode.Statements.OfType<SettingsBlockNode>().First();
        var envBlocks = fileNode.Statements.OfType<EnvBlockNode>().ToList();

        // Evaluate base settings with global scope
        var baseSettings = this.EvaluateBlock(settingsBlock.Block, globalScope);

        // Evaluate environment overlays
        var environmentOverlays = new Dictionary<string, JsonObject>();

        foreach (var envBlock in envBlocks)
        {
            // Create child scope for this environment
            var envScope = globalScope.CreateChild();
            
            // Evaluate let statements in env block (if any in the future)
            // For now, env blocks only contain settings blocks
            
            var envSettings = this.EvaluateBlock(envBlock.SettingsBlock.Block, envScope);
            environmentOverlays[envBlock.EnvironmentName] = envSettings;
        }

        return new(baseSettings, environmentOverlays);
    }

    /// <summary>
    ///     Evaluates let statements and adds variables to the scope.
    /// </summary>
    private void EvaluateLetStatements(IEnumerable<ITopLevelStatement> statements, VariableScope scope)
    {
        var expressionEvaluator = new ExpressionEvaluator(scope);

        foreach (var statement in statements.OfType<LetNode>())
        {
            var value = expressionEvaluator.Evaluate(statement.Value);
            scope.Define(statement.Name, value);
        }
    }

    /// <summary>
    ///     Validates the structure of the file:
    ///     - Exactly one settings block at top level
    ///     - No duplicate env names
    ///     - No duplicate let variable names at same scope level
    /// </summary>
    private void ValidateStructure(FileNode fileNode)
    {
        var settingsBlocks = fileNode.Statements.OfType<SettingsBlockNode>().ToList();

        // Must have exactly one settings block
        if (settingsBlocks.Count == 0)
        {
            throw new EvaluatorException(
                "File must contain exactly one 'settings' block",
                fileNode.Location
            );
        }

        if (settingsBlocks.Count > 1)
        {
            throw new EvaluatorException(
                "File must contain exactly one 'settings' block, but found multiple",
                settingsBlocks[1].Location
            );
        }

        // Check for duplicate env names
        var envBlocks = fileNode.Statements.OfType<EnvBlockNode>().ToList();
        var envNames = new HashSet<string>();

        foreach (var envBlock in envBlocks)
        {
            if (!envNames.Add(envBlock.EnvironmentName))
            {
                throw new EvaluatorException(
                    $"Duplicate environment name '{envBlock.EnvironmentName}'",
                    envBlock.Location
                );
            }
        }

        // Check for duplicate let variable names
        var letStatements = fileNode.Statements.OfType<LetNode>().ToList();
        var varNames = new HashSet<string>();

        foreach (var letStmt in letStatements)
        {
            if (!varNames.Add(letStmt.Name))
            {
                throw new EvaluatorException(
                    $"Duplicate variable name '{letStmt.Name}'",
                    letStmt.Location
                );
            }
        }
    }

    /// <summary>
    ///     Evaluates a block and returns a JsonObject.
    /// </summary>
    private JsonObject EvaluateBlock(BlockNode block, VariableScope scope)
    {
        var result = new JsonObject();

        // Evaluate any let statements in the block first
        this.EvaluateLetStatements(block.Statements.OfType<ITopLevelStatement>(), scope);

        foreach (var statement in block.Statements)
        {
            if (statement is AssignmentNode assignment)
            {
                this.EvaluateAssignment(result, assignment, scope);
            }
            else if (statement is NestedBlockNode nestedBlock)
            {
                this.EvaluateNestedBlock(result, nestedBlock, scope);
            }
            // LetNode is already handled above
        }

        return result;
    }

    /// <summary>
    ///     Evaluates an assignment statement and adds it to the target object.
    ///     Handles dot-path assignments (e.g., A.B.C = value).
    /// </summary>
    private void EvaluateAssignment(JsonObject target, AssignmentNode assignment, VariableScope scope)
    {
        var path = assignment.Path.Segments;

        // Navigate to the parent object, creating intermediate objects as needed
        var current = target;

        for (var i = 0; i < path.Count - 1; i++)
        {
            var segment = path[i];

            if (!current.ContainsKey(segment))
            {
                // Create intermediate object
                current[segment] = new JsonObject();
            }

            var next = current[segment];

            if (next is not JsonObject nextObj)
            {
                throw new EvaluatorException(
                    $"Cannot traverse path '{string.Join(".", path)}': '{segment}' is not an object",
                    assignment.Location
                );
            }

            current = nextObj;
        }

        // Set the final value
        var finalKey = path[^1];
        var value = this.EvaluateValue(assignment.Value, scope);
        current[finalKey] = value;
    }

    /// <summary>
    ///     Evaluates a nested block statement and adds it to the target object.
    /// </summary>
    private void EvaluateNestedBlock(JsonObject target, NestedBlockNode nestedBlock, VariableScope scope)
    {
        var childObject = this.EvaluateBlock(nestedBlock.Block, scope);
        target[nestedBlock.Name] = childObject;
    }

    /// <summary>
    ///     Evaluates a value/expression node and returns a JsonNode.
    /// </summary>
    private JsonNode? EvaluateValue(IExpression expression, VariableScope scope)
    {
        // Use ExpressionEvaluator to convert IExpression → RuntimeValue
        var expressionEvaluator = new ExpressionEvaluator(scope);
        var runtimeValue = expressionEvaluator.Evaluate(expression);
        
        // Then convert RuntimeValue → JsonNode
        return this.ConvertRuntimeValueToJson(runtimeValue);
    }

    /// <summary>
    ///     Converts a RuntimeValue to JsonNode.
    /// </summary>
    private JsonNode? ConvertRuntimeValueToJson(RuntimeValue value)
    {
        return value switch
        {
            StringValue s => JsonValue.Create(s.Value),
            NumberValue n => this.ConvertNumberToJson(n.Value),
            BoolValue b => JsonValue.Create(b.Value),
            NullValue => null,
            ArrayValue arr => this.ConvertArrayToJson(arr),
            ObjectValue obj => this.ConvertObjectToJson(obj),
            _ => throw new EvaluatorException($"Unsupported runtime value type: {value.GetType().Name}", location: null)
        };
    }

    private JsonValue ConvertNumberToJson(decimal value)
    {
        // If the value is an integer (no fractional part), convert to long
        // Otherwise, convert to double
        if (value == decimal.Floor(value) && value >= long.MinValue && value <= long.MaxValue)
        {
            return JsonValue.Create((long)value);
        }
        else
        {
            return JsonValue.Create((double)value);
        }
    }

    private JsonArray ConvertArrayToJson(ArrayValue array)
    {
        var result = new JsonArray();
        foreach (var item in array.Items)
        {
            result.Add(this.ConvertRuntimeValueToJson(item));
        }
        return result;
    }

    private JsonObject ConvertObjectToJson(ObjectValue obj)
    {
        var result = new JsonObject();
        foreach (var (key, value) in obj.Properties)
        {
            result[key] = this.ConvertRuntimeValueToJson(value);
        }
        return result;
    }

    /// <summary>
    ///     Evaluates a literal node and returns a JsonValue.
    /// </summary>
    private JsonNode? EvaluateLiteral(LiteralNode literal)
    {
        return literal.Value switch
        {
            null => null,
            string s => JsonValue.Create(s),
            long l => JsonValue.Create(l),
            double d => JsonValue.Create(d),
            bool b => JsonValue.Create(b),
            _ => throw new EvaluatorException(
                     $"Unsupported literal type: {literal.Value.GetType().Name}",
                     literal.Location
                 ),
        };
    }
}
