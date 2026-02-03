namespace Settex.Core.Parser.Ast;

using Settex.Core.Diagnostics;

/// <summary>
///     Tagged object value: ident { ... }
///     Example in array: [ service { Host = "localhost" } ]
///     This constructs a JSON object as a value.
/// </summary>
public sealed record TaggedObjectNode(
    string Tag,
    BlockNode Block,
    SourceLocation Location) : IValue;
