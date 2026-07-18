# Settex for Visual Studio Code

**Settex** is a declarative configuration language compiler for .NET that transforms `*.settex` source files into `appsettings*.json` files.

This extension provides rich language support for Settex files in Visual Studio Code.

📖 **Documentation:** [settex.74nu5.dev](https://settex.74nu5.dev)

## Features

### 🎨 Syntax Highlighting
- Complete syntax highlighting for all Settex V2 features
- Keywords: `settings`, `env`, `include`, `let`, `for`, `if`
- Operators: `=`, `:=`, `+`, `-`, `*`, `/`, `==`, `!=`, `<`, `>`, `??`
- String interpolation: `"Hello ${name}"`
- Comments: `#` and `//`

### ✨ Code Snippets
- `settings` - Create a settings block
- `env` - Create an environment overlay
- `let` - Define a variable
- `for` - Create a for loop
- `include` - Include another file
- `!settex` - Complete file template
- And more...

### 🔧 Editor Features
- Bracket matching and auto-closing
- Auto-indentation
- Comment toggling (`Ctrl+/`)
- Code folding

## Settex Language Features

Settex V2 supports:
- ✅ **Include system**: Reuse configurations across files
- ✅ **Variables (`let`)**: Define and reuse values
- ✅ **Expressions**: Arithmetic, logical, comparison operations
- ✅ **String interpolation**: `"Port: ${port}"`
- ✅ **Conditional assignments**: `if` inline expressions
- ✅ **Set-if-missing operator**: `:=` for defaults
- ✅ **For loops**: Generate array items from collections

## Example

```settex
# common.settex
let defaultPort = 5000
let logLevel = "Information"

settings {
    ApplicationName = "MyApp"
    Server.Port = defaultPort
    Logging.LogLevel.Default = logLevel
}

env "Development" {
    settings {
        Server.Port = 5001
        Logging.LogLevel.Default = "Debug"
    }
}

env "Production" {
    settings {
        Server.Port = 443
        Logging.LogLevel.Default = "Warning"
    }
}
```

## Requirements

- Visual Studio Code 1.85.0 or higher

## Extension Settings

This extension contributes the following settings:

* `settex.trace.server`: Traces the communication between VS Code and the language server (future)

## Known Issues

- Language Server Protocol integration (coming in Phase 3)
- IntelliSense and diagnostics (coming in Phase 4)

## Release Notes

### 0.1.0 (Initial Release)

- ✅ Syntax highlighting for Settex V2
- ✅ Code snippets
- ✅ Bracket matching and auto-closing
- ✅ Comment toggling

## For More Information

* [Settex GitHub Repository](https://github.com/yourusername/settex)
* [Settex Documentation](https://github.com/yourusername/settex/blob/main/README.md)

---

**Enjoy using Settex!** 🚀
