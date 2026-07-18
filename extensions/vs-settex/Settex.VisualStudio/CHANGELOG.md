# Changelog - Settex Visual Studio Extension

All notable changes to the Settex Visual Studio extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-02-08

### Added
- **Automatic Build Integration**: Automatically compile .settex files when saved
  - New Options page: **Tools > Options > Settex > General**
  - Toggle "Compile on Save" (enabled by default)
  - Configure success/error notifications
  - Log compilation output to dedicated "Settex" output pane
- **Settings/Options Page**:
  - Compile on Save: Enable/disable auto-compile (default: enabled)
  - Show Success Notifications: Display message box on successful compilation (default: disabled)
  - Show Error Notifications: Display message box on compilation errors (default: enabled)
  - Log to Output Window: Write compilation messages to Output pane (default: enabled)
- **Output Window Integration**:
  - Dedicated "Settex" output pane for compilation messages
  - Real-time compilation status and error messages

### Changed
- Manual compilation (Tools > Compile Settex File) now shows error dialogs
- Auto-compile on save only logs to Output window (no dialogs unless configured)
- Enhanced build service to support both interactive and automatic compilation modes

## [1.1.0] - 2026-02-07

### Added
- **Code Snippets**: 11 built-in code snippets for common Settex patterns
  - `settings` - Settings block
  - `env` - Environment overlay
  - `let` - Variable declaration
  - `for` - For loop in array
  - `include` - Include file
  - `interp` - String interpolation
  - `tag` - Tagged object
  - `array` - Array literal
  - `setif` - Set if missing (`:=` operator)
  - `block` - Nested block
  - `settex` - Complete file template
- **Language Server Protocol (LSP) Integration**:
  - IntelliSense with code completion
  - Hover information for variables
  - Real-time diagnostics and error checking
  - Go to definition support
  - Powered by Settex.LanguageServer
- **Build Integration**:
  - Manual compilation via **Tools > Compile Settex File** menu
  - Build service for compiling `.settex` files
  - Integration with Settex.Cli
- **Visual Studio 2026 Support**:
  - Compatible with Visual Studio 2022-2026 (versions 17.0-19.0)

### Changed
- Updated extension description to reflect new features
- Enhanced documentation with usage examples for new features
- Improved package initialization for better reliability

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
