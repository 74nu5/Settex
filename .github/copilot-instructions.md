# Copilot Instructions for Settex

## Project Overview

**Settex** (Settings + Syntax + Extension) is a declarative configuration language compiler for .NET that transforms `*.settex` source files into `appsettings*.json` files. 

**Current Status**: V2.0 - 9/10 phases complete, 174/174 tests passing ✅

## Build, Test, and Lint Commands

### Build
```bash
# Build entire solution
dotnet build

# Build without restore
dotnet build --no-restore

# Build specific project
dotnet build src/Settex.Core
```

### Test
```bash
# Run all tests (174 tests)
dotnet test

# Run tests without build
dotnet test --no-build

# Run tests with verbosity
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~LexerTests"
```

### Restore
```bash
# Restore packages (uses Central Package Management)
dotnet restore
```

**Note**: TreatWarningsAsErrors is enabled - no warnings allowed in builds.

## Architecture

Settex follows a classic compiler pipeline with V2 enhancements:

```
Source (.settex) 
    ↓
[Include Resolver] ──→ Merged Source (cycle detection)
    ↓
[Lexer] ─────────────→ Tokens (50+ token types)
    ↓
[Parser] ────────────→ AST (18 node types + Pratt parser for expressions)
    ↓
[Evaluator] ─────────→ SettingsModel (base + env overlays, variable scopes)
    ↓
[Merger] ────────────→ JSON Models per environment (deep merge)
    ↓
[JsonWriter] ────────→ appsettings*.json (deterministic output)
```

### Project Structure
- **Settex.Core**: Main compiler library (multi-targets: net10.0, netstandard2.0) ✅
- **Settex.Build**: MSBuild task integration ✅
- **Settex.Cli**: CLI tool with Spectre.Console ✅
- **Settex.LanguageServer**: LSP implementation (in progress)

### Key Components (All Complete)

#### Lexer ✅
- **Location**: `src/Settex.Core/Lexer/`
- **Files**: `Lexer.cs`, `Token.cs`, `TokenType.cs`, `LexerException.cs`
- **Key feature**: Bracket depth tracking (`bracketDepth`) determines newline significance
  - Inside arrays `[...]`: newlines become `Newline` tokens
  - Outside arrays: newlines treated as whitespace
- **Number parsing**: Uses `CultureInfo.InvariantCulture` for consistent parsing
- **Comments**: Both `#` and `//` supported (skipped, not emitted)
- **V2 tokens**: Operators (`+`, `-`, `*`, `/`, `==`, `!=`, `and`, `or`, `not`, `??`), `let`, `if`, `for`, `in`, `:=`, `include`

#### Parser ✅
- **Location**: `src/Settex.Core/Parser/`
- **Algorithm**: Recursive descent + Pratt parser for expressions
- **Expression precedence**: 9 levels (logicalOr → logicalAnd → coalesce → equality → comparison → term → factor → unary → primary)
- **AST nodes in `Parser/Ast/`**: `FileNode`, `SettingsBlockNode`, `EnvBlockNode`, `IncludeNode`, `LetNode`, `AssignmentNode` (with conditional + operator), `ForNode`, `InterpolatedStringNode`, etc.
- **String interpolation**: Detects `${...}` in lexer, parses expressions inside
- See `specs/specifications-V1.md` and `specs/specifications-V2.md` for grammar

#### Resolution ✅
- **Location**: `src/Settex.Core/Resolution/`
- **IncludeResolver**: Resolves relative paths, detects include cycles with stack-based algorithm
- **Errors**: `IncludeException` with precise location info

#### Runtime ✅
- **Location**: `src/Settex.Core/Runtime/`
- **RuntimeValue hierarchy**: `StringValue`, `NumberValue`, `BoolValue` (with .True/.False constants), `NullValue`, `ArrayValue`, `ObjectValue`
- **Used by**: Expression evaluator for type-safe evaluation

#### Evaluator ✅
- **Location**: `src/Settex.Core/Evaluation/`
- **Variable scopes**: Lexical scoping (global → env → for loop)
- **Expression evaluation**: Full support for arithmetic, logical, comparison, coalesce operations
- **Implicit `env` variable**: Set to "Base" for base, or environment name for overlays
- **Conditional assignments**: `Path = Value if Condition` evaluated at assignment time
- **Set-if-missing**: `:=` operator checks both base and overlay before assigning

#### Merger ✅
- **Location**: `src/Settex.Core/Merging/`
- **Rules**: Deep merge for objects, replacement for primitives and arrays
- **Type checking**: Validates compatible types during merge

#### Writing ✅
- **Location**: `src/Settex.Core/Writing/`
- **JsonWriter**: Deterministic JSON output with consistent formatting
- **Conditional writes**: Only writes file if content changed

#### Compilation ✅
- **Location**: `src/Settex.Core/Compilation/`
- **SettexCompiler**: Facade orchestrating entire pipeline
- **CompilationResult**: Contains success flag, diagnostics (errors/warnings/info), generated file list
- **Diagnostic codes**: STX001-STX302 (see implementation plan)

## Key Conventions

### Multi-targeting
- All Settex.Core code must work on both **net10.0** and **netstandard2.0**
- For netstandard2.0 compatibility: `IsExternalInit.cs` polyfill enables C# records
- Conditional compilation: Use `#if NETSTANDARD2_0` when needed
- Use `Meziantou.Polyfill` package for additional polyfills

### Central Package Management
- **Directory.Packages.props** manages all package versions
- **NEVER** add `Version` attributes to `<PackageReference>` in .csproj files
- Add new packages to Directory.Packages.props first
- Current key packages: TUnit 1.12.125, System.Text.Json 10.0.2, MSBuild 17.12.50

### Testing Framework
- Uses **TUnit 1.12.125** (not xUnit, not NUnit)
- All test methods must be `async Task` (TUnit requirement)
- Assertions: `await Assert.That(value).IsEqualTo(expected)`
- Exception assertions: `await Assert.ThrowsAsync<ExceptionType>(() => Task.FromResult(action()))`
- **No FluentAssertions, no Verify** - TUnit has built-in assertions
- Test organization mirrors source: `tests/Settex.Core.Tests/Lexer/LexerTests.cs` for `src/Settex.Core/Lexer/Lexer.cs`

### Code Style (CRITICAL - Enforced by .editorconfig)
- **Records** for immutable data (Token, SourceLocation, AST nodes, RuntimeValue types)
- **Nullable reference types** enabled globally - handle nulls explicitly
- **TreatWarningsAsErrors** enabled - no warnings allowed in builds
- **Interface naming**: Must start with `I` prefix (`IAstNode`, `IExpression`, `IValue`)
- **Private fields**: NO underscore prefix (use `position`, not `_position`)
- **Member access**: ALWAYS use `this.` for fields, properties, and methods
  - ✅ `this.position`, `this.Advance()`, `this.Current`
  - ❌ `position`, `Advance()`, `Current`
  - Exception: In constructors when assigning parameters: `this.source = source`
- **Namespaces**: Must follow file path hierarchy exactly
  - File: `src/Settex.Core/Lexer/Lexer.cs` → Namespace: `namespace Settex.Core.Lexer;`
  - File: `src/Settex.Core/Runtime/RuntimeValue.cs` → Namespace: `namespace Settex.Core.Runtime;`
- **Using directives**: MUST be inside the namespace declaration (file-scoped namespaces)
- **Braces**: Always on new lines (Allman style) per .editorconfig
- See `.editorconfig` for complete formatting rules

### File Organization
```
src/Settex.Core/
├── Compilation/     # SettexCompiler facade, CompilationResult, Diagnostic
├── Lexer/          # Tokenization (50+ token types)
├── Parser/         # AST generation + Pratt parser
│   └── Ast/        # 18 AST node types (FileNode, ExpressionNode, etc.)
├── Evaluation/     # AST → SettingsModel with variable scopes
├── Merging/        # Deep merge logic for JSON
├── Writing/        # JSON serialization (deterministic)
├── Resolution/     # Include resolution + cycle detection
├── Runtime/        # RuntimeValue type system (6 value types)
├── Diagnostics/    # SourceLocation, error codes STX001-STX302
└── IsExternalInit.cs  # netstandard2.0 polyfill for records

src/Settex.Build/
├── CompileSettexTask.cs  # MSBuild task
└── build/
    ├── Settex.Build.props    # Build properties
    └── Settex.Build.targets  # Build targets

src/Settex.Cli/
└── Program.cs  # CLI with System.CommandLine + Spectre.Console

tests/Settex.Core.Tests/
├── Lexer/          # Lexer tests
├── Parser/         # Parser + expression tests
├── Evaluation/     # Evaluator, variables, expressions, conditionals
├── Merging/        # Merge logic tests
├── Writing/        # JSON writer tests
├── Compilation/    # End-to-end compiler tests
└── Integration/    # Integration tests with golden files
```

### Determinism Requirement
- All output must be **deterministic**: identical input → identical output
- JSON key ordering must be preserved
- No random elements, no timestamps in generated files
- Number parsing uses `CultureInfo.InvariantCulture`
- File writes use UTF-8 without BOM

### Diagnostic Codes
- All errors/warnings use codes: STX001-STX302
- Format: `STX<category><number>` (e.g., STX101 for lexer errors)
- Categories: 00x (structure), 10x (lexer), 20x (semantic), 30x (I/O)
- See implementation plan for full code allocation

## V2 Advanced Features

Settex V2 adds SASS-like capabilities:

### Include System
- Syntax: `include "./common.settex"`
- Relative path resolution from current file
- Stack-based cycle detection (error on circular includes)
- Implementation: `src/Settex.Core/Resolution/IncludeResolver.cs`

### Variables & Scopes
- Syntax: `let basePort = 5000`
- Lexical scoping: global → env → for loop
- Variable resolution with parent scope lookup
- Implementation: `src/Settex.Core/Runtime/` (RuntimeValue hierarchy)

### Expressions (Pratt Parser)
- **9 precedence levels**: logicalOr → logicalAnd → coalesce → equality → comparison → term → factor → unary → primary
- **Arithmetic**: `+`, `-`, `*`, `/` (numeric only)
- **Comparison**: `==`, `!=`, `<`, `>`, `<=`, `>=`
- **Logical**: `and`, `or`, `not` (boolean only, short-circuit evaluation)
- **Coalesce**: `??` (returns right if left is null)
- **Implementation**: `Parser.ParseExpression()` with recursive precedence climbing

### String Interpolation
- Syntax: `"http://${host}:${port}/api"`
- Expressions evaluated and converted to strings
- Null in interpolation → error (explicit handling required)
- Types auto-convert: numbers, bools → string
- Implementation: `InterpolatedStringNode` with segments

### Conditional Assignments
- Syntax: `LogLevel = "Debug" if env == "Development"`
- Implicit `env` variable: "Base" or environment name
- Condition must evaluate to boolean
- False condition → assignment skipped
- Implementation: `AssignmentNode.Condition`

### Set-If-Missing Operator
- Syntax: `Port := 8080` (only sets if key doesn't exist)
- In base: checks if key exists in base settings
- In env: checks both base AND current overlay
- Works with conditional: `Port := 8080 if env == "Production"`
- Implementation: `AssignmentNode.Op` (Set vs SetIfMissing)

### For Loops
- Syntax: `for item in collection { ... }` (array context only)
- Iterator variable scoped to loop body
- Collection must evaluate to array (error otherwise)
- Generated items flattened into parent array
- Example:
  ```settex
  Services = [
    for svc in serviceList {
      service { Name = svc.name Port = svc.port }
    }
  ]
  ```

## Reference Documentation

- **Specifications**: 
  - `specs/specifications-V1.md` - V1 language spec (settings, env, arrays, objects)
  - `specs/specifications-V2.md` - V2 advanced features (include, let, expressions, for, if)
- **Implementation Plans**: 
  - `plans/2026-02-03-settex-v1-implementation.md` - V1 plan (10 phases, all complete ✅)
  - `plans/2026-02-03-settex-v2-implementation.md` - V2 plan (10 phases, 9/10 complete ✅)
- **Current Work**: Check phase status in V2 implementation plan

## Development Workflows

### Adding a New Feature

1. **Update specs** first: Add to `specs/specifications-V2.md` (or V3)
2. **Update implementation plan**: Add phase/tasks to relevant plan file
3. **Add token types** if needed: Update `TokenType` enum, lexer scanning
4. **Add AST nodes**: Create in `Parser/Ast/`, implement `IAstNode` interface
5. **Update parser**: Add parsing logic, update precedence if expression-related
6. **Update evaluator**: Add evaluation logic, handle new AST nodes
7. **Write tests**: Unit tests first, then integration tests
8. **Test end-to-end**: Create sample in `samples/` or integration test
9. **Update README**: Document new syntax and examples

### Debugging Tips

- **Token stream**: Call `lexer.Tokenize()` to inspect all tokens
- **AST structure**: Parser returns `FileNode` - inspect in debugger
- **Variable scopes**: Check `VariableScope.TryGet()` for variable resolution
- **Expression evaluation**: Use `ExpressionEvaluator.Evaluate()` with RuntimeValues
- **Diagnostics**: Always include `SourceLocation` for precise error reporting
- **Test specific phase**: Use `--filter` with dotnet test

### Common Patterns

#### Creating AST Nodes
```csharp
// All AST nodes are records with SourceLocation
public sealed record MyNode(
    SomeType Property,
    SourceLocation Location
) : IAstNode;
```

#### Parsing with Error Recovery
```csharp
if (!this.Match(TokenType.Expected))
{
    throw new ParserException(
        "Expected token X",
        this.Current.Location
    );
}
```

#### Evaluating Expressions
```csharp
// Evaluate expression to RuntimeValue
var value = ExpressionEvaluator.Evaluate(expr, scope);

// Check type
if (value is not BoolValue boolVal)
{
    throw new EvaluationException(
        "Expected boolean",
        expr.Location
    );
}
```

#### Variable Scopes
```csharp
// Create child scope
var childScope = new VariableScope(parentScope);

// Define variable
childScope.Define("name", value);

// Lookup (checks parent chain)
if (scope.TryGet("name", out var value)) { ... }
```

## Current Development Status

**Version**: V2.0 (9/10 phases complete)
**Tests**: 174/174 passing ✅
**Next**: Phase 10 - Documentation & Samples completion

Refer to `plans/2026-02-03-settex-v2-implementation.md` for detailed status and next tasks.
