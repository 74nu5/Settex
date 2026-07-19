namespace Settex.Core.Evaluation;

using System.Text.Json.Nodes;

using Settex.Core.Diagnostics;
using Settex.Core.Merging;
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
    /// <param name="fileNode">Root of the (include-flattened) AST to evaluate.</param>
    /// <param name="requireSettingsBlock">
    ///     Whether the file must contain at least one <c>settings</c> block. True when
    ///     compiling, because a compilation root that produces no configuration is a
    ///     mistake. False when analysing a file that is only <i>a</i> file — an include
    ///     fragment contributing <c>let</c> variables has no <c>settings</c> block by
    ///     design, and flagging it would be wrong.
    /// </param>
    public SettingsModel Evaluate(FileNode fileNode, bool requireSettingsBlock = true)
    {
        // Validate structure
        this.ValidateStructure(fileNode, requireSettingsBlock);

        // Create global scope and evaluate global let statements
        var globalScope = new VariableScope();
        this.EvaluateLetStatements(fileNode.Statements, globalScope);

        // Collect settings blocks and env blocks. Multiple settings blocks may
        // appear once includes are flattened; they are deep-merged in document
        // order (later blocks win), which is what makes modular configuration
        // via includes work.
        var settingsBlocks = fileNode.Statements.OfType<SettingsBlockNode>().ToList();
        var envBlocks = fileNode.Statements.OfType<EnvBlockNode>().ToList();

        // Define implicit 'env' variable for base settings
        globalScope.Define("env", new StringValue("Base"));

        var merger = new Merger();

        // Evaluate and deep-merge all base settings blocks in document order.
        // Each block sees the accumulated result as its base so that ':=' and
        // nested-object merges behave as if the blocks were written together.
        var baseSettings = new JsonObject();

        foreach (var settingsBlock in settingsBlocks)
        {
            var context = baseSettings.Count > 0 ? baseSettings : null;
            var evaluated = this.EvaluateBlock(settingsBlock.Block, globalScope, context);
            baseSettings = SafeMerge(merger, baseSettings, evaluated, settingsBlock.Location);
        }

        // Evaluate environment overlays, deep-merging blocks that target the
        // same environment (again, so an include can contribute to an env).
        // Case-insensitive, because .NET's own environment model is: IsDevelopment and
        // IsEnvironment compare with OrdinalIgnoreCase, so env "Dev" and env "dev" are
        // one environment there. Comparing ordinally here made them two overlays that
        // then raced for a single appsettings.Dev.json on Windows — one silently
        // overwrote the other, under a success message listing both files. They now
        // deep-merge, exactly as two blocks spelled identically already did.
        var environmentOverlays = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var environmentLocations = new Dictionary<string, SourceLocation>(StringComparer.OrdinalIgnoreCase);

        foreach (var envBlock in envBlocks)
        {
            // Remember where each environment was first declared, to locate any
            // base/overlay type conflict reported below.
            if (!environmentLocations.ContainsKey(envBlock.EnvironmentName))
            {
                environmentLocations[envBlock.EnvironmentName] = envBlock.Location;
            }

            // Create child scope for this environment
            var envScope = globalScope.CreateChild();

            // Define implicit 'env' variable with environment name
            envScope.Define("env", new StringValue(envBlock.EnvironmentName));

            // Context for ':=' / nested merges: the base plus any overlay already
            // accumulated for this same environment.
            var context = environmentOverlays.TryGetValue(envBlock.EnvironmentName, out var prior)
                ? SafeMerge(merger, baseSettings, prior, envBlock.Location)
                : baseSettings;

            var envSettings = this.EvaluateBlock(envBlock.SettingsBlock.Block, envScope, context);

            environmentOverlays[envBlock.EnvironmentName] = prior is null
                ? envSettings
                : SafeMerge(merger, prior, envSettings, envBlock.Location);
        }

        // Validate every overlay against the base, independently of the output mode.
        // Merged output would surface a conflict when it merges; delta output never
        // merges base and overlay, so without this check it would silently emit an
        // incoherent pair of files (e.g. base "Foo": 1 next to an overlay "Foo": {…}).
        foreach (var (envName, overlay) in environmentOverlays)
        {
            ValidateOverlayAgainstBase(baseSettings, overlay, envName, environmentLocations[envName], string.Empty);
        }

        return new(baseSettings, environmentOverlays);
    }

    /// <summary>
    ///     Walks an environment overlay against the base settings and throws on the
    ///     first type conflict (a key that is an object on one side and a primitive or
    ///     array on the other). Uses <see cref="Merger.AreCompatible" /> so the rule is
    ///     exactly the one the merge itself applies.
    /// </summary>
    private static void ValidateOverlayAgainstBase(
        JsonObject baseObject,
        JsonObject overlay,
        string environmentName,
        SourceLocation location,
        string path)
    {
        foreach (var (key, overlayValue) in overlay)
        {
            var currentPath = path.Length == 0 ? key : $"{path}.{key}";

            // A key the base doesn't define can never conflict.
            if (!TryGetPropertyIgnoreCase(baseObject, key, out var baseValue))
            {
                continue;
            }

            if (baseValue is JsonObject baseChild && overlayValue is JsonObject overlayChild)
            {
                ValidateOverlayAgainstBase(baseChild, overlayChild, environmentName, location, currentPath);
                continue;
            }

            if (Merger.AreCompatible(baseValue, overlayValue))
            {
                continue;
            }

            throw new EvaluatorException(
                $"Type mismatch for key '{currentPath}' in environment '{environmentName}': " +
                $"base is {Merger.DescribeNodeType(baseValue)}, overlay is {Merger.DescribeNodeType(overlayValue)}",
                location);
        }
    }

    /// <summary>
    ///     Looks a property up the way .NET configuration does: case-insensitively.
    ///     <see cref="JsonObject" /> compares ordinally, so a base <c>Foo</c> and an
    ///     overlay <c>foo</c> looked like two independent keys here — which meant the
    ///     one case where they collide at runtime, and therefore the one case worth
    ///     reporting, was the one that slipped through unchecked.
    /// </summary>
    private static bool TryGetPropertyIgnoreCase(JsonObject source, string key, out JsonNode? value)
    {
        if (source.TryGetPropertyValue(key, out value))
        {
            return true;
        }

        foreach (var (candidate, candidateValue) in source)
        {
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
            {
                value = candidateValue;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    ///     Merges two settings objects, converting a <see cref="MergerException" />
    ///     (e.g. a type conflict between blocks — common when includes contribute a
    ///     block) into a located <see cref="EvaluatorException" />, so the compiler
    ///     reports it as a normal diagnostic with a source location rather than an
    ///     unlocated "Unexpected error".
    /// </summary>
    private static JsonObject SafeMerge(Merger merger, JsonObject baseObject, JsonObject overlay, SourceLocation location)
    {
        try
        {
            return merger.Merge(baseObject, overlay);
        }
        catch (MergerException ex)
        {
            throw new EvaluatorException(ex.Message, ex.Location ?? location);
        }
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
    ///     - At least one settings block at top level, when
    ///       <paramref name="requireSettingsBlock" /> says so
    ///     - No duplicate let variable names at same scope level
    ///     Multiple settings blocks and multiple env blocks with the same name
    ///     are allowed: they are deep-merged in document order, which is what
    ///     lets an included file contribute a settings block.
    /// </summary>
    private void ValidateStructure(FileNode fileNode, bool requireSettingsBlock)
    {
        var settingsBlocks = fileNode.Statements.OfType<SettingsBlockNode>().ToList();

        // Must have at least one settings block (across the file and its includes)
        if (requireSettingsBlock && settingsBlocks.Count == 0)
        {
            throw new EvaluatorException(
                "File must contain at least one 'settings' block",
                fileNode.Location
            );
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
    /// <param name="block">The block to evaluate.</param>
    /// <param name="scope">The variable scope to use.</param>
    /// <param name="baseSettings">Base settings object (null when evaluating base settings itself, non-null when evaluating env overlays).</param>
    private JsonObject EvaluateBlock(BlockNode block, VariableScope scope, JsonObject? baseSettings)
    {
        var result = new JsonObject();

        // Evaluate any let statements in the block first
        this.EvaluateLetStatements(block.Statements.OfType<ITopLevelStatement>(), scope);

        foreach (var statement in block.Statements)
        {
            if (statement is AssignmentNode assignment)
            {
                this.EvaluateAssignment(result, assignment, scope, baseSettings);
            }
            else if (statement is NestedBlockNode nestedBlock)
            {
                this.EvaluateNestedBlock(result, nestedBlock, scope, baseSettings);
            }
            // LetNode is already handled above
        }

        return result;
    }

    /// <summary>
    ///     Evaluates an assignment statement and adds it to the target object.
    ///     Handles dot-path assignments (e.g., A.B.C = value).
    ///     Supports conditional assignments (e.g., Path = Value if Condition).
    ///     Supports set-if-missing operator (:=).
    /// </summary>
    private void EvaluateAssignment(JsonObject target, AssignmentNode assignment, VariableScope scope, JsonObject? baseSettings)
    {
        // Check if there's a condition
        if (assignment.Condition != null)
        {
            var expressionEvaluator = new ExpressionEvaluator(scope);
            var conditionValue = expressionEvaluator.Evaluate(assignment.Condition);

            if (conditionValue is not BoolValue boolValue)
            {
                throw new EvaluatorException(
                    $"Condition in 'if' must be a boolean, got {conditionValue.GetType().Name}",
                    assignment.Location
                );
            }

            // If condition is false, skip this assignment
            if (!boolValue.Value)
            {
                return;
            }
        }

        var path = assignment.Path.Segments;

        // For := operator, check if key already exists
        if (assignment.Op == AssignmentOp.SetIfMissing)
        {
            // Check if the key exists in current overlay or base settings
            if (this.PathExists(target, path) || (baseSettings != null && this.PathExists(baseSettings, path)))
            {
                // Key exists, skip this assignment
                return;
            }
        }

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
    ///     Checks if a path exists in a JsonObject.
    /// </summary>
    private bool PathExists(JsonObject obj, IReadOnlyList<string> path)
    {
        var current = obj;

        for (var i = 0; i < path.Count; i++)
        {
            var segment = path[i];

            // Case-insensitive, like every configuration key comparison. Comparing
            // ordinally here meant ':=' could not see a base value spelled differently
            // and assigned anyway — the one operator whose entire contract is not to
            // override was the one overriding.
            if (!TryGetPropertyIgnoreCase(current, segment, out var next))
            {
                return false;
            }

            // If this is the last segment, the path exists
            if (i == path.Count - 1)
            {
                return true;
            }

            if (next is not JsonObject nextObj)
            {
                // Path exists but the value is not an object, so the full path doesn't exist
                return false;
            }

            current = nextObj;
        }

        return true;
    }

    /// <summary>
    ///     Evaluates a nested block statement and adds it to the target object.
    /// </summary>
    private void EvaluateNestedBlock(JsonObject target, NestedBlockNode nestedBlock, VariableScope scope, JsonObject? baseSettings)
    {
        // For nested blocks, we need to find the corresponding nested object in base settings (if any)
        JsonObject? baseNestedObject = null;
        
        if (baseSettings != null && TryGetPropertyIgnoreCase(baseSettings, nestedBlock.Name, out var baseValue))
        {
            baseNestedObject = baseValue as JsonObject;
        }

        // Evaluate the nested block in a child scope so that 'let' bindings declared
        // inside it stay local (they can still read outer variables, but no longer
        // leak into — and overwrite — the parent/global scope).
        var childScope = scope.CreateChild();
        var childObject = this.EvaluateBlock(nestedBlock.Block, childScope, baseNestedObject);

        // A block whose assignments were all skipped — every condition false — evaluates
        // to an empty object. Writing it made the overlay look like an object and
        // collide with a primitive of the same name in the base, failing the build over
        // a configuration that overrides nothing at all.
        //
        // The distinction that matters is intent, and the AST carries it: a block the
        // author wrote empty has no statements, while one emptied by its conditions had
        // statements that were all skipped. Only the latter is dropped. An empty object
        // is *not* invisible at runtime — .NET surfaces it as a key with a null value,
        // so dropping an explicitly empty block would change observable configuration.
        if (childObject.Count == 0 && nestedBlock.Block.Statements.Count > 0)
        {
            return;
        }

        // Two blocks of the same name in one scope deep-merge, like everything else in
        // the language: `Db { Host = … }` followed by `Db { Port = … }` used to keep
        // only the second, silently losing Host. Includes make that shape easy to reach
        // without noticing.
        if (TryGetPropertyIgnoreCase(target, nestedBlock.Name, out var existing) && existing is JsonObject existingObject)
        {
            target[nestedBlock.Name] = SafeMerge(new Merger(), existingObject, childObject, nestedBlock.Location);
            return;
        }

        target[nestedBlock.Name] = childObject;
    }

    /// <summary>
    ///     Evaluates a value/expression node and returns a JsonNode.
    /// </summary>
    private JsonNode? EvaluateValue(IExpression expression, VariableScope scope)
    {
        // Special handling for ArrayNode with ForNode elements
        if (expression is ArrayNode arrayNode && arrayNode.Elements.Any(e => e is ForNode))
        {
            return this.EvaluateArrayWithForLoops(arrayNode, scope);
        }
        
        // Use ExpressionEvaluator to convert IExpression → RuntimeValue
        var expressionEvaluator = new ExpressionEvaluator(scope);
        var runtimeValue = expressionEvaluator.Evaluate(expression);
        
        // Then convert RuntimeValue → JsonNode
        return this.ConvertRuntimeValueToJson(runtimeValue);
    }

    /// <summary>
    ///     Evaluates an array that contains for loops.
    /// </summary>
    private JsonArray EvaluateArrayWithForLoops(ArrayNode arrayNode, VariableScope scope)
    {
        var result = new JsonArray();
        var expressionEvaluator = new ExpressionEvaluator(scope);

        foreach (var element in arrayNode.Elements)
        {
            if (element is IExpression expr)
            {
                // Regular expression element
                var value = expressionEvaluator.Evaluate(expr);
                var jsonValue = this.ConvertRuntimeValueToJson(value);
                result.Add(jsonValue);
            }
            else if (element is ForNode forNode)
            {
                // For loop - evaluate and add all generated items
                var forItems = this.EvaluateForLoop(forNode, scope);
                foreach (var item in forItems)
                {
                    result.Add(item);
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Evaluates a for loop and returns the generated JSON items.
    /// </summary>
    private List<JsonNode?> EvaluateForLoop(ForNode forNode, VariableScope scope)
    {
        var results = new List<JsonNode?>();

        // Evaluate the collection expression
        var expressionEvaluator = new ExpressionEvaluator(scope);
        var collectionValue = expressionEvaluator.Evaluate(forNode.Collection);

        if (collectionValue is not ArrayValue arrayValue)
        {
            throw new EvaluatorException(
                $"For loop collection must be an array, got {collectionValue.GetType().Name}",
                forNode.Location
            );
        }

        // Iterate over each element in the collection
        foreach (var item in arrayValue.Items)
        {
            // Create child scope with iterator variable
            var forScope = scope.CreateChild();
            forScope.Define(forNode.IteratorName, item);

            // Evaluate the body and collect results
            var bodyResult = this.EvaluateForBody(forNode.Body, forScope);
            results.Add(bodyResult);
        }

        return results;
    }

    /// <summary>
    ///     Evaluates a for loop body and returns the generated JSON object.
    ///     The body should contain a single nested block (tagged object like "item { ... }").
    /// </summary>
    private JsonNode? EvaluateForBody(BlockNode body, VariableScope scope)
    {
        // The body should contain exactly one nested block (item { ... })
        var nestedBlocks = body.Statements.OfType<NestedBlockNode>().ToList();

        if (nestedBlocks.Count == 0)
        {
            throw new EvaluatorException(
                "For loop body must contain a tagged object (e.g., 'item { ... }')",
                body.Location
            );
        }

        if (nestedBlocks.Count > 1)
        {
            throw new EvaluatorException(
                "For loop body can only contain one tagged object",
                body.Location
            );
        }

        // Evaluate the nested block
        var nestedBlock = nestedBlocks[0];
        var obj = this.EvaluateBlock(nestedBlock.Block, scope, baseSettings: null);
        return obj;
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
