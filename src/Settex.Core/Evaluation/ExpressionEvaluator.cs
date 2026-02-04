namespace Settex.Core.Evaluation;

using System.Text;

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
        => this.scope = scope;

    /// <summary>
    ///     Evaluates an expression to a runtime value.
    /// </summary>
    public RuntimeValue Evaluate(IExpression expression)
    {
        return expression switch
        {
            LiteralNode literal => this.EvaluateLiteral(literal),
            VariableRefNode varRef => this.EvaluateVariableRef(varRef),
            MemberAccessNode memberAccess => this.EvaluateMemberAccess(memberAccess),
            ArrayNode array => this.EvaluateArray(array),
            TaggedObjectNode obj => this.EvaluateTaggedObject(obj),
            BinaryOpNode binOp => this.EvaluateBinaryOp(binOp),
            UnaryOpNode unaryOp => this.EvaluateUnaryOp(unaryOp),
            InterpolatedStringNode interpolated => this.EvaluateInterpolatedString(interpolated),
            _ => throw new EvaluatorException($"Unsupported expression type: {expression.GetType().Name}", null),
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
            _ => throw new EvaluatorException($"Unsupported literal type: {literal.Value?.GetType().Name}", literal.Location),
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

    private RuntimeValue EvaluateMemberAccess(MemberAccessNode memberAccess)
    {
        // Evaluate the object expression first
        var objValue = this.Evaluate(memberAccess.Object);

        // Member access only works on objects
        if (objValue is not ObjectValue obj)
        {
            throw new EvaluatorException(
                $"Cannot access member '{memberAccess.MemberName}' on non-object type {objValue.GetType().Name}",
                memberAccess.Location
            );
        }

        // Get the member value
        if (!obj.Properties.TryGetValue(memberAccess.MemberName, out var memberValue))
        {
            throw new EvaluatorException(
                $"Object does not have a member '{memberAccess.MemberName}'",
                memberAccess.Location
            );
        }

        return memberValue;
    }

    private RuntimeValue EvaluateArray(ArrayNode array)
    {
        var items = new List<RuntimeValue>();

        foreach (var element in array.Elements)
        {
            if (element is IExpression expr)
            {
                var value = this.Evaluate(expr);
                items.Add(value);
            }
            else if (element is ForNode forNode)
            {
                // For loops should be handled by Evaluator, not ExpressionEvaluator
                throw new EvaluatorException(
                    "For loops in arrays should be evaluated in Evaluator context",
                    forNode.Location
                );
            }
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

    private RuntimeValue EvaluateBinaryOp(BinaryOpNode binOp)
    {
        var left = this.Evaluate(binOp.Left);
        
        // Short-circuit for logical operators
        if (binOp.Operator == "and")
        {
            if (left is not BoolValue leftBool)
            {
                throw new EvaluatorException($"Operator 'and' requires boolean operands, got {left.GetType().Name}", binOp.Location);
            }

            if (!leftBool.Value)
            {
                return BoolValue.False;
            }

            var right = this.Evaluate(binOp.Right);

            if (right is not BoolValue rightBool)
            {
                throw new EvaluatorException($"Operator 'and' requires boolean operands, got {right.GetType().Name}", binOp.Location);
            }

            return rightBool.Value ? BoolValue.True : BoolValue.False;
        }

        if (binOp.Operator == "or")
        {
            if (left is not BoolValue leftBool)
            {
                throw new EvaluatorException($"Operator 'or' requires boolean operands, got {left.GetType().Name}", binOp.Location);
            }

            if (leftBool.Value)
            {
                return BoolValue.True;
            }

            var right = this.Evaluate(binOp.Right);

            if (right is not BoolValue rightBool)
            {
                throw new EvaluatorException($"Operator 'or' requires boolean operands, got {right.GetType().Name}", binOp.Location);
            }

            return rightBool.Value ? BoolValue.True : BoolValue.False;
        }

        // Null coalescing: return left if not null, otherwise right
        if (binOp.Operator == "??")
        {
            if (left is not NullValue)
            {
                return left;
            }

            return this.Evaluate(binOp.Right);
        }

        // For other operators, evaluate both sides
        var rightValue = this.Evaluate(binOp.Right);

        return binOp.Operator switch
        {
            "+" => this.EvaluateAddition(left, rightValue, binOp.Location),
            "-" => this.EvaluateSubtraction(left, rightValue, binOp.Location),
            "*" => this.EvaluateMultiplication(left, rightValue, binOp.Location),
            "/" => this.EvaluateDivision(left, rightValue, binOp.Location),
            "==" => this.EvaluateEquality(left, rightValue),
            "!=" => this.EvaluateInequality(left, rightValue),
            "<" => this.EvaluateLessThan(left, rightValue, binOp.Location),
            "<=" => this.EvaluateLessThanOrEqual(left, rightValue, binOp.Location),
            ">" => this.EvaluateGreaterThan(left, rightValue, binOp.Location),
            ">=" => this.EvaluateGreaterThanOrEqual(left, rightValue, binOp.Location),
            _ => throw new EvaluatorException($"Unknown binary operator '{binOp.Operator}'", binOp.Location),
        };
    }

    private RuntimeValue EvaluateUnaryOp(UnaryOpNode unaryOp)
    {
        var operand = this.Evaluate(unaryOp.Operand);

        return unaryOp.Operator switch
        {
            "not" => this.EvaluateNot(operand, unaryOp.Location),
            "-" => this.EvaluateNegation(operand, unaryOp.Location),
            _ => throw new EvaluatorException($"Unknown unary operator '{unaryOp.Operator}'", unaryOp.Location),
        };
    }

    // Arithmetic operators
    private RuntimeValue EvaluateAddition(RuntimeValue left, RuntimeValue right, SourceLocation location)
    {
        if (left is NumberValue leftNum && right is NumberValue rightNum)
        {
            return new NumberValue(leftNum.Value + rightNum.Value);
        }

        throw new EvaluatorException($"Operator '+' requires numeric operands, got {left.GetType().Name} and {right.GetType().Name}", location);
    }

    private RuntimeValue EvaluateSubtraction(RuntimeValue left, RuntimeValue right, SourceLocation location)
    {
        if (left is NumberValue leftNum && right is NumberValue rightNum)
        {
            return new NumberValue(leftNum.Value - rightNum.Value);
        }

        throw new EvaluatorException($"Operator '-' requires numeric operands, got {left.GetType().Name} and {right.GetType().Name}", location);
    }

    private RuntimeValue EvaluateMultiplication(RuntimeValue left, RuntimeValue right, SourceLocation location)
    {
        if (left is NumberValue leftNum && right is NumberValue rightNum)
        {
            return new NumberValue(leftNum.Value * rightNum.Value);
        }

        throw new EvaluatorException($"Operator '*' requires numeric operands, got {left.GetType().Name} and {right.GetType().Name}", location);
    }

    private RuntimeValue EvaluateDivision(RuntimeValue left, RuntimeValue right, SourceLocation location)
    {
        if (left is NumberValue leftNum && right is NumberValue rightNum)
        {
            if (rightNum.Value == 0)
            {
                throw new EvaluatorException("Division by zero", location);
            }

            return new NumberValue(leftNum.Value / rightNum.Value);
        }

        throw new EvaluatorException($"Operator '/' requires numeric operands, got {left.GetType().Name} and {right.GetType().Name}", location);
    }

    private RuntimeValue EvaluateNegation(RuntimeValue operand, SourceLocation location)
    {
        if (operand is NumberValue num)
        {
            return new NumberValue(-num.Value);
        }

        throw new EvaluatorException($"Operator '-' requires numeric operand, got {operand.GetType().Name}", location);
    }

    // Comparison operators
    private RuntimeValue EvaluateEquality(RuntimeValue left, RuntimeValue right)
    {
        // Null equality
        if (left is NullValue && right is NullValue)
        {
            return BoolValue.True;
        }

        if (left is NullValue || right is NullValue)
        {
            return BoolValue.False;
        }

        // Number equality
        if (left is NumberValue leftNum && right is NumberValue rightNum)
        {
            return leftNum.Value == rightNum.Value ? BoolValue.True : BoolValue.False;
        }

        // String equality
        if (left is StringValue leftStr && right is StringValue rightStr)
        {
            return leftStr.Value == rightStr.Value ? BoolValue.True : BoolValue.False;
        }

        // Bool equality
        if (left is BoolValue leftBool && right is BoolValue rightBool)
        {
            return leftBool.Value == rightBool.Value ? BoolValue.True : BoolValue.False;
        }

        // Different types are not equal
        return BoolValue.False;
    }

    private RuntimeValue EvaluateInequality(RuntimeValue left, RuntimeValue right)
    {
        var equality = this.EvaluateEquality(left, right);
        return equality is BoolValue boolVal && boolVal.Value ? BoolValue.False : BoolValue.True;
    }

    private RuntimeValue EvaluateLessThan(RuntimeValue left, RuntimeValue right, SourceLocation location)
    {
        if (left is NumberValue leftNum && right is NumberValue rightNum)
        {
            return leftNum.Value < rightNum.Value ? BoolValue.True : BoolValue.False;
        }

        throw new EvaluatorException($"Operator '<' requires numeric operands, got {left.GetType().Name} and {right.GetType().Name}", location);
    }

    private RuntimeValue EvaluateLessThanOrEqual(RuntimeValue left, RuntimeValue right, SourceLocation location)
    {
        if (left is NumberValue leftNum && right is NumberValue rightNum)
        {
            return leftNum.Value <= rightNum.Value ? BoolValue.True : BoolValue.False;
        }

        throw new EvaluatorException($"Operator '<=' requires numeric operands, got {left.GetType().Name} and {right.GetType().Name}", location);
    }

    private RuntimeValue EvaluateGreaterThan(RuntimeValue left, RuntimeValue right, SourceLocation location)
    {
        if (left is NumberValue leftNum && right is NumberValue rightNum)
        {
            return leftNum.Value > rightNum.Value ? BoolValue.True : BoolValue.False;
        }

        throw new EvaluatorException($"Operator '>' requires numeric operands, got {left.GetType().Name} and {right.GetType().Name}", location);
    }

    private RuntimeValue EvaluateGreaterThanOrEqual(RuntimeValue left, RuntimeValue right, SourceLocation location)
    {
        if (left is NumberValue leftNum && right is NumberValue rightNum)
        {
            return leftNum.Value >= rightNum.Value ? BoolValue.True : BoolValue.False;
        }

        throw new EvaluatorException($"Operator '>=' requires numeric operands, got {left.GetType().Name} and {right.GetType().Name}", location);
    }

    // Logical operators
    private RuntimeValue EvaluateNot(RuntimeValue operand, SourceLocation location)
    {
        if (operand is BoolValue boolVal)
        {
            return boolVal.Value ? BoolValue.False : BoolValue.True;
        }

        throw new EvaluatorException($"Operator 'not' requires boolean operand, got {operand.GetType().Name}", location);
    }

    private RuntimeValue EvaluateInterpolatedString(InterpolatedStringNode interpolated)
    {
        var sb = new StringBuilder();

        foreach (var segment in interpolated.Segments)
        {
            switch (segment)
            {
                case LiteralSegment literal:
                    sb.Append(literal.Text);
                    break;

                case ExpressionSegment exprSeg:
                    var value = this.Evaluate(exprSeg.Expression);

                    if (value is NullValue)
                    {
                        throw new EvaluatorException("Interpolated expression cannot be null", interpolated.Location);
                    }

                    var strValue = value switch
                    {
                        StringValue s => s.Value,
                        NumberValue n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        BoolValue b => b.Value ? "true" : "false",
                        _ => throw new EvaluatorException($"Cannot interpolate value of type {value.GetType().Name}", interpolated.Location),
                    };

                    sb.Append(strValue);
                    break;
            }
        }

        return new StringValue(sb.ToString());
    }
}
