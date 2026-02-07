# Changelog - Settex Visual Studio Extension

All notable changes to the Settex Visual Studio extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- Language Server Protocol integration for IntelliSense
- Build integration with automatic .settex compilation
- Error diagnostics in Error List window
- Code snippets for common Settex patterns
- Quick fixes for common issues
- Go to definition support
- Find all references
- Rename refactoring

## [1.0.0] - 2026-02-07

### Added
- Initial release of Visual Studio extension for Settex
- **Syntax Highlighting**:
  - TextMate grammar-based syntax highlighting
  - Support for all Settex V2 keywords: `settings`, `env`, `include`, `let`, `for`, `in`, `if`
  - Operator highlighting: `=`, `:=`, arithmetic, comparison, logical, coalesce
  - String interpolation syntax: `"${variable}"`
  - Comment styles: `#` and `//`
  - Constants: `true`, `false`, `null`
  - Numbers (integers and floats)
  - Strings with escape sequences

- **Editor Features**:
  - Automatic bracket matching for `{}`, `[]`, `()`
  - Auto-closing pairs for brackets and quotes
  - Auto-indentation for nested blocks
  - Comment toggling support (Ctrl+/ or Ctrl+K, Ctrl+C)
  - File extension association for `.settex` files
  - Language configuration (brackets, comments, indentation rules)

- **Extension Infrastructure**:
  - Modern SDK-style VSIX project using CodingWithCalvin.VsixSdk
  - Package registration via .pkgdef
  - TextMate repository registration
  - Editor factory configuration
  - Visual Studio 2022+ compatibility (version 17.0 - 19.0)
  - Support for Community, Professional, and Enterprise editions

- **Documentation**:
  - Comprehensive README with features and installation instructions
  - BUILDING.md with detailed build guide
  - LICENSE.txt (MIT License)
  - Extension manifest with proper metadata

### Technical Details
- Target Framework: .NET Framework 4.7.2
- Visual Studio SDK: 17.*
- VSIX Build Tools: 17.*
- Package GUID: CF2F7AA1-CFD1-4FBD-9A5E-6BA5B3FE5ED8
- Extension ID: Settex.VisualStudio.0A84A5C9-8EF5-4C2E-BB59-5650C105306A

### Notes
- This extension requires Windows and Visual Studio 2022+ to build and run
- TextMate grammar is shared with VS Code extension for consistency
- Language Server integration will be added in a future release

## Links

- [Repository](https://github.com/74nu5/Settex)
- [Issues](https://github.com/74nu5/Settex/issues)
- [Settex Documentation](https://github.com/74nu5/Settex#readme)

---

[Unreleased]: https://github.com/74nu5/Settex/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/74nu5/Settex/releases/tag/v1.0.0
