namespace Settex.Core.Evaluation;

using Settex.Core.Diagnostics;
using Settex.Core.Parser.Ast;
using Settex.Core.Runtime;

/// <summary>
///     Evaluates expressions to runtime values.
/// </summary>
public class ExpressionEvaluator
{
    private readonly VariableScope scope;

    public ExpressionEvaluator(VariableScope scope)
    {
        this.scope = scope;
    }

    /// <summary>
    ///     Evaluates an expression to a runtime value.
    /// </summary>
    public RuntimeValue Evaluate(IExpression expression)
    {
        return expression switch
        {
            LiteralNode literal => this.EvaluateLiteral(literal),
            VariableRefNode varRef => this.EvaluateVariableRef(varRef),
            ArrayNode array => this.EvaluateArray(array),
            TaggedObjectNode obj => this.EvaluateTaggedObject(obj),
            _ => throw new EvaluatorException($"Unsupported expression type: {expression.GetType().Name}", location: null)
        };
    }

    private RuntimeValue EvaluateLiteral(LiteralNode literal)
    {
        return literal.Value switch
        {
            string s => new StringValue(s),
            long l => new NumberValue(l),
            double d => new NumberValue((decimal)d),
            bool b => new BoolValue(b),
            null => NullValue.Instance,
            _ => throw new EvaluatorException($"Unsupported literal type: {literal.Value?.GetType().Name}", literal.Location)
        };
    }

    private RuntimeValue EvaluateVariableRef(VariableRefNode varRef)
    {
        var value = this.scope.Lookup(varRef.Name);
        
        if (value == null)
        {
            throw new EvaluatorException($"Variable '{varRef.Name}' is not defined", varRef.Location);
        }

        return value;
    }

    private RuntimeValue EvaluateArray(ArrayNode array)
    {
        var items = new List<RuntimeValue>();

        foreach (var item in array.Items)
        {
            var value = this.Evaluate(item);
            items.Add(value);
        }

        return new ArrayValue(items);
    }

    private RuntimeValue EvaluateTaggedObject(TaggedObjectNode obj)
    {
        var properties = new Dictionary<string, RuntimeValue>();

        foreach (var statement in obj.Block.Statements)
        {
            if (statement is AssignmentNode assignment)
            {
                var path = string.Join(".", assignment.Path.Segments);
                var value = this.Evaluate(assignment.Value);
                
                // For now, simple flat assignment (no nested paths in objects)
                // This will be enhanced in later phases
                if (assignment.Path.Segments.Count == 1)
                {
                    properties[assignment.Path.Segments[0]] = value;
                }
                else
                {
                    throw new EvaluatorException("Nested paths not supported in tagged object expressions yet", assignment.Location);
                }
            }
        }

        return new ObjectValue(properties);
    }
}
