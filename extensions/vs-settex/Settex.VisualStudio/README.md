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

### Build Integration (Coming Soon)
- Automatic compilation of `.settex` files during build
- Error diagnostics in the Error List window
- Quick fixes for common issues

### IntelliSense (Coming Soon)
- Code completion for keywords and variables
- Hover information for variable values
- Signature help for function-like constructs

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

1. Create or open a `.settex` file in your project
2. The extension automatically provides syntax highlighting
3. Use standard Visual Studio editor features (IntelliSense, bracket matching, etc.)
4. Build your project to compile `.settex` files to `appsettings.json`

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

- Visual Studio 2022 (version 17.0) or later
- .NET Framework 4.7.2 or later

## Supported Visual Studio Editions

- Visual Studio Community
- Visual Studio Professional
- Visual Studio Enterprise

## Known Issues

- Language server integration is in progress
- Some IntelliSense features are not yet available

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

### 1.0.0 (Initial Release)

- ✨ Syntax highlighting for `.settex` files
- ✨ TextMate grammar support
- ✨ Bracket matching and auto-indentation
- ✨ Comment toggling support
- ✨ Visual Studio 2022+ compatibility

