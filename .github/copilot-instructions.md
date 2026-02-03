# Copilot Instructions for Settex

## Project Overview

**Settex** (Settings + Syntax + Extension) is a declarative configuration language compiler for .NET that transforms `*.settex` source files into `appsettings*.json` files. The project is in early development (Phase 2 of 10 complete).

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
# Run all tests
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

**Note**: No linting is configured yet. TreatWarningsAsErrors is enabled in Directory.Build.props.

## Architecture

Settex follows a classic compiler pipeline:

```
Source (.settex) 
    ↓
[Lexer] ──────→ Tokens
    ↓
[Parser] ─────→ AST
    ↓
[Evaluator] ──→ SettingsModel (base + overlays)
    ↓
[Merger] ─────→ JSON Models per environment
    ↓
[JsonWriter] ─→ appsettings*.json
```

### Project Structure
- **Settex.Core**: Main compiler library (multi-targets: net10.0, netstandard2.0)
- **Settex.Build**: MSBuild task integration (planned Phase 8)
- **Settex.Cli**: CLI tool (planned Phase 9)

### Key Components (Current State)

#### Lexer (✅ Complete - Phase 2)
- **Location**: `src/Settex.Core/Lexer/`
- **Files**: `Lexer.cs`, `Token.cs`, `TokenType.cs`, `LexerException.cs`
- **Key feature**: Bracket depth tracking (`_bracketDepth`) determines newline significance
  - Inside arrays `[...]`: newlines become `Newline` tokens
  - Outside arrays: newlines treated as whitespace
- **Number parsing**: Uses `CultureInfo.InvariantCulture` for consistent parsing across locales
- **Comments**: Both `#` and `//` supported (skipped, not emitted as tokens)

#### Diagnostics
- **Location**: `src/Settex.Core/Diagnostics/`
- **SourceLocation**: Tracks FilePath, Line, Column, Length for all tokens/AST nodes

#### Parser (⏳ Planned - Phase 3)
- Will use recursive descent parser
- See `specs/specifications.md` for EBNF grammar
- AST nodes will include: FileNode, SettingsBlockNode, EnvBlockNode, etc.

## Key Conventions

### Multi-targeting
- All Settex.Core code must work on both **net10.0** and **netstandard2.0**
- For netstandard2.0 compatibility: `IsExternalInit.cs` polyfill enables C# records
- Conditional compilation: Use `#if NETSTANDARD2_0` when needed

### Central Package Management
- **Directory.Packages.props** manages all package versions
- Never add `Version` attributes to `<PackageReference>` in .csproj files
- Add new packages to Directory.Packages.props first

### Testing Framework
- Uses **TUnit 0.3.0** (not xUnit, not NUnit)
- All test methods must be `async Task` (TUnit requirement)
- Assertions: `await Assert.That(value).IsEqualTo(expected)`
- Exception assertions: `await Assert.ThrowsAsync<ExceptionType>(() => Task.FromResult(action()))`
- **No FluentAssertions, no Verify** - TUnit has built-in assertions

### Code Style
- **Records** for immutable data (Token, SourceLocation, future AST nodes)
- **Nullable reference types** enabled globally
- **TreatWarningsAsErrors** enabled - no warnings allowed
- **Interface naming**: Must start with `I` prefix
- **Private fields**: NO underscore prefix (use `position`, not `_position`)
- **Member access**: ALWAYS use `this.` for fields, properties, and methods
  - ✅ `this.position`, `this.Advance()`, `this.Current`
  - ❌ `position`, `Advance()`, `Current`
- **Namespaces**: Must follow file path hierarchy exactly
  - File: `src/Settex.Core/Lexer/Lexer.cs` → Namespace: `namespace Settex.Core.Lexer;`
  - File: `src/Settex.Core/Diagnostics/SourceLocation.cs` → Namespace: `namespace Settex.Core.Diagnostics;`
- **Using inside namespace**: All `using` directives must be inside the namespace declaration
- See `.editorconfig` for C# formatting rules (braces on new lines, etc.)

### File Organization
```
src/Settex.Core/
├── Lexer/          # Tokenization
├── Parser/         # AST generation
│   └── Ast/        # AST node classes
├── Evaluation/     # AST → SettingsModel
├── Merging/        # Deep merge logic
├── Output/         # JSON serialization
├── Diagnostics/    # SourceLocation, error codes
└── IsExternalInit.cs  # netstandard2.0 polyfill

tests/Settex.Core.Tests/
├── Lexer/          # Lexer tests
├── Parser/         # Parser tests (planned)
└── Integration/    # End-to-end tests (planned)
```

### Determinism Requirement
- All output must be **deterministic**: identical input → identical output
- JSON key ordering must be preserved
- No random elements, no timestamps in generated files

### Diagnostic Codes
- All errors/warnings will use codes: STX001-STX302
- Format: `STX<category><number>` (e.g., STX101 for lexer errors)
- See implementation plan for full code allocation

## Reference Documentation

- **Specifications**: `specs/specifications.md` - Full V1 language spec
- **Implementation Plan**: `plans/2026-02-03-settex-v1-implementation.md` - 10-phase plan with checkboxes
- **Session Plan**: `~/.copilot/session-state/*/plan.md` - Current work tracking

## Current Development Phase

**Phase 2: Lexer** ✅ Complete (17 tests passing)

**Next Phase: Phase 3 - Parser + AST**
- Create all AST node classes (FileNode, SettingsBlockNode, etc.)
- Implement recursive descent parser following EBNF grammar
- Parser tests with comprehensive coverage

Refer to `plans/2026-02-03-settex-v1-implementation.md` for detailed task breakdowns.
