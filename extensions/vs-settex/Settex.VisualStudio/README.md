# Settex Visual Studio Extension

Visual Studio extension for the Settex configuration language, providing syntax highlighting, IntelliSense, and integrated build support for `.settex` files.

## Features

### Syntax Highlighting
- Full syntax highlighting for Settex configuration files
- Support for all Settex V2 features:
  - Keywords: `settings`, `env`, `include`, `let`, `for`, `in`, `if`
  - Operators: `=`, `:=`, `+`, `-`, `*`, `/`, `==`, `!=`, `<`, `>`, `<=`, `>=`, `and`, `or`, `not`, `??`
  - String interpolation: `"${variable}"`
  - Comments: `#` and `//`
  - Constants: `true`, `false`, `null`

### Editor Support
- Automatic bracket matching and pairing
- Auto-indentation for nested blocks
- Comment toggling (Ctrl+/ or Ctrl+K, Ctrl+C)
- Folding regions for settings and environment blocks

### Code Snippets
- **11 built-in snippets** for common Settex patterns:
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
- Type snippet prefix and press Tab to expand

### IntelliSense & Language Server
- **Code completion** for keywords and variables
- **Hover information** for variable values
- **Go to definition** support
- **Diagnostics and error checking** in real-time
- Powered by Settex.LanguageServer (LSP)

### Build Integration
- **Manual compilation** via Tools menu:
  - Open a `.settex` file
  - Go to **Tools > Compile Settex File**
  - Generates `appsettings*.json` files
- **Automatic build integration** (via MSBuild task)
  - Add `<PackageReference Include="Settex.Build" />` to your project
  - `.settex` files compile automatically during build
- **Error diagnostics** shown in Error List window

## Installation

### From Visual Studio Marketplace
1. Open Visual Studio 2022 or later
2. Go to **Extensions > Manage Extensions**
3. Search for "Settex"
4. Click **Download** and restart Visual Studio

### From VSIX File
1. Download the latest `.vsix` file from [Releases](https://github.com/74nu5/Settex/releases)
2. Double-click the `.vsix` file to install
3. Restart Visual Studio

## Usage

### Basic Editing
1. Create or open a `.settex` file in your project
2. The extension automatically provides:
   - Syntax highlighting
   - IntelliSense (code completion)
   - Bracket matching
3. Use code snippets (type prefix + Tab):
   - Type `settings` + Tab for a settings block
   - Type `env` + Tab for an environment overlay
   - Type `let` + Tab for a variable declaration

### Compiling Settex Files

#### Manual Compilation
1. Open a `.settex` file
2. Go to **Tools > Compile Settex File**
3. Generated `appsettings*.json` files appear in the same directory

#### Automatic Build Integration
Add to your `.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="Settex.Build" Version="2.0.0" />
</ItemGroup>
```

Then build your project - `.settex` files compile automatically!

## Example

```settex
# Example Settex configuration
let baseUrl = "http://localhost"
let port = 8000

settings {
  ApplicationName = "MyApp"
  BaseUrl = "${baseUrl}:${port}"
  
  Logging {
    LogLevel {
      Default = "Information"
    }
  }
}

env Development {
  let port = 5000
  Logging.LogLevel.Default = "Debug"
}

env Production {
  let baseUrl = "https://api.example.com"
  let port = 443
  Logging.LogLevel.Default = "Warning"
}
```

## Requirements

- Visual Studio 2022 (version 17.0) or later, **including Visual Studio 2026**
- .NET Framework 4.7.2 or later (for the extension itself)
- Optional: Settex.LanguageServer for IntelliSense (auto-detected if available)
- Optional: Settex.Build for automatic compilation during build

## Supported Visual Studio Editions

- Visual Studio Community
- Visual Studio Professional
- Visual Studio Enterprise

## Known Issues

- Language server features require Settex.LanguageServer to be available
- Build integration requires Settex.Cli to be available or Settex.Build package installed
- Some advanced IntelliSense features are still in development

## Contributing

Contributions are welcome! Please see the [main repository](https://github.com/74nu5/Settex) for contribution guidelines.

## License

This extension is licensed under the MIT License - see the [LICENSE](https://github.com/74nu5/Settex/blob/main/LICENSE) file for details.

## Links

- [Settex Repository](https://github.com/74nu5/Settex)
- [Settex Documentation](https://github.com/74nu5/Settex#readme)
- [VS Code Extension](https://marketplace.visualstudio.com/items?itemName=74nu5.vscode-settex)
- [Report Issues](https://github.com/74nu5/Settex/issues)

## Changelog

### 1.1.0 (Latest)

- ✨ **Code Snippets**: 11 built-in snippets for common patterns
- ✨ **Language Server Integration**: IntelliSense with code completion, hover, and diagnostics
- ✨ **Build Integration**: Manual compilation via Tools menu
- ✨ Visual Studio 2026 support
- 📝 Updated documentation and testing guides

### 1.0.0 (Initial Release)

- ✨ Syntax highlighting for `.settex` files
- ✨ TextMate grammar support
- ✨ Bracket matching and auto-indentation
- ✨ Comment toggling support
- ✨ Visual Studio 2022+ compatibility

