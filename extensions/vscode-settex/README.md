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

### 🧠 Language Server (requires the .NET 10 runtime)
- Hover showing evaluated variable values and per-environment overlays
- Go to definition and find all references for variables and environments (across `include`d files)
- Live diagnostics from the compiler
- Context-aware completion

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
- **.NET 10 runtime** — required by the bundled Settex language server (IntelliSense, diagnostics, hover, go-to-definition). Without it, syntax highlighting and snippets still work; the extension detects the missing runtime and shows an actionable message with a download link. Install from [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0).

## Extension Settings

This extension contributes the following settings:

* `settex.trace.server`: Traces the communication between VS Code and the language server.

## Release Notes

- ✅ Syntax highlighting for Settex V2
- ✅ Code snippets, bracket matching, comment toggling
- ✅ Language server: hover, go-to-definition, find references, diagnostics, completion (needs the .NET 10 runtime)

## For More Information

* [Settex GitHub Repository](https://github.com/74nu5/Settex)
* [Settex Documentation](https://settex.74nu5.dev)

---

**Enjoy using Settex!** 🚀
