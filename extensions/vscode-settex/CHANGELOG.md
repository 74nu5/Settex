# Change Log

All notable changes to the Settex extension will be documented in this file.

## [0.1.0] - 2026-02-04

### Added
- Initial release of Settex VS Code extension
- Complete syntax highlighting for Settex V2
- TextMate grammar with support for:
  - Keywords (settings, env, include, let, for, if)
  - Operators (=, :=, +, -, *, /, ==, !=, <, >, ??)
  - String interpolation with `${...}`
  - Comments (# and //)
  - Numbers (integers and floats)
  - Boolean and null constants
- 12 code snippets for common Settex patterns
- Language configuration:
  - Bracket matching for {}, [], ()
  - Auto-closing pairs
  - Comment toggling
  - Auto-indentation rules
  - Code folding support

### Planned
- Language Server Protocol integration (Phase 3)
- IntelliSense and autocompletion (Phase 4)
- Real-time diagnostics and error checking (Phase 3)
- Hover information (Phase 5)
- Go to definition (Phase 6)
- Code formatting (Phase 7)
