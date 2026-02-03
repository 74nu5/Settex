namespace Settex.Core.Evaluation;

using System.Text.Json.Nodes;

using Settex.Core.Parser.Ast;

/// <summary>
///     Evaluates an AST and produces a SettingsModel.
///     Performs validation and converts AST nodes to JSON representation.
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

        // Find the settings block and env blocks
        var settingsBlock = fileNode.Statements.OfType<SettingsBlockNode>().First();
        var envBlocks = fileNode.Statements.OfType<EnvBlockNode>().ToList();

        // Evaluate base settings
        var baseSettings = this.EvaluateBlock(settingsBlock.Block);

        // Evaluate environment overlays
        var environmentOverlays = new Dictionary<string, JsonObject>();

        foreach (var envBlock in envBlocks)
        {
            var envSettings = this.EvaluateBlock(envBlock.SettingsBlock.Block);
            environmentOverlays[envBlock.EnvironmentName] = envSettings;
        }

        return new(baseSettings, environmentOverlays);
    }

    /// <summary>
    ///     Validates the structure of the file:
    ///     - Exactly one settings block at top level
    ///     - No duplicate env names
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
    }

    /// <summary>
    ///     Evaluates a block and returns a JsonObject.
    /// </summary>
    private JsonObject EvaluateBlock(BlockNode block)
    {
        var result = new JsonObject();

        foreach (var statement in block.Statements)
        {
            if (statement is AssignmentNode assignment)
            {
                this.EvaluateAssignment(result, assignment);
            }
            else if (statement is NestedBlockNode nestedBlock)
            {
                this.EvaluateNestedBlock(result, nestedBlock);
            }
        }

        return result;
    }

    /// <summary>
    ///     Evaluates an assignment statement and adds it to the target object.
    ///     Handles dot-path assignments (e.g., A.B.C = value).
    /// </summary>
    private void EvaluateAssignment(JsonObject target, AssignmentNode assignment)
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
        var value = this.EvaluateValue(assignment.Value);
        current[finalKey] = value;
    }

    /// <summary>
    ///     Evaluates a nested block statement and adds it to the target object.
    /// </summary>
    private void EvaluateNestedBlock(JsonObject target, NestedBlockNode nestedBlock)
    {
        var childObject = this.EvaluateBlock(nestedBlock.Block);
        target[nestedBlock.Name] = childObject;
    }

    /// <summary>
    ///     Evaluates a value node and returns a JsonNode.
    /// </summary>
    private JsonNode? EvaluateValue(IValue value)
    {
        return value switch
        {
            LiteralNode literal => this.EvaluateLiteral(literal),
            ArrayNode array => this.EvaluateArray(array),
            TaggedObjectNode taggedObj => this.EvaluateTaggedObject(taggedObj),
            _ => throw new EvaluatorException(
                     $"Unknown value type: {value.GetType().Name}",
                     value.Location
                 ),
        };
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

    /// <summary>
    ///     Evaluates an array node and returns a JsonArray.
    /// </summary>
    private JsonArray EvaluateArray(ArrayNode array)
    {
        var result = new JsonArray();

        foreach (var item in array.Items)
        {
            var value = this.EvaluateValue(item);
            result.Add(value);
        }

        return result;
    }

    /// <summary>
    ///     Evaluates a tagged object node and returns a JsonObject.
    ///     The tag is ignored in V1 (only used for syntax).
    /// </summary>
    private JsonObject EvaluateTaggedObject(TaggedObjectNode taggedObj)
        => this.EvaluateBlock(taggedObj.Block);
}
