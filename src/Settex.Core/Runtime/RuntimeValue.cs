namespace Settex.Core.Runtime;

/// <summary>
///     Base type for all runtime values in Settex expressions.
/// </summary>
public abstract record RuntimeValue;

/// <summary>
///     String value.
/// </summary>
public sealed record StringValue(string Value) : RuntimeValue;

/// <summary>
///     Number value (decimal for precision).
/// </summary>
public sealed record NumberValue(decimal Value) : RuntimeValue;

/// <summary>
///     Boolean value.
/// </summary>
public sealed record BoolValue(bool Value) : RuntimeValue;

/// <summary>
///     Null value.
/// </summary>
public sealed record NullValue : RuntimeValue
{
    public static readonly NullValue Instance = new();
}

/// <summary>
///     Array value.
/// </summary>
public sealed record ArrayValue(List<RuntimeValue> Items) : RuntimeValue;

/// <summary>
///     Object value.
/// </summary>
public sealed record ObjectValue(Dictionary<string, RuntimeValue> Properties) : RuntimeValue;
