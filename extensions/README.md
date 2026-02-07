# Settex Editor Extensions

This directory contains editor extensions for Settex, providing IDE support for `.settex` configuration files.

## Available Extensions

### 1. Visual Studio Code Extension

**Location**: `vscode-settex/`

A fully-featured VS Code extension with syntax highlighting, language server integration, and IntelliSense support.

**Features**:
- ✅ Syntax highlighting
- ✅ Language server integration
- ✅ Code completion
- ✅ Diagnostics and error checking
- ✅ Hover information
- ✅ Code snippets

**Installation**:
- From VS Code Marketplace: Search for "Settex"
- From source: See `vscode-settex/README.md`

**Platforms**: Windows, macOS, Linux

---

### 2. Visual Studio 2022+ Extension

**Location**: `vs-settex/Settex.VisualStudio/`

A Visual Studio extension providing syntax highlighting and editor support for Visual Studio 2022 and later.

**Features**:
- ✅ Syntax highlighting (TextMate grammar)
- ✅ Bracket matching and auto-closing
- ✅ Auto-indentation
- ✅ Comment toggling
- 🔄 Language server integration (planned)
- 🔄 Build integration (planned)

**Installation**:
- From Visual Studio Marketplace: Search for "Settex"
- From source: See `vs-settex/BUILDING.md`

**Platforms**: Windows only (requires Visual Studio 2022+)

**Note**: This extension requires Windows and .NET Framework 4.7.2 for building and running.

---

## Quick Comparison

| Feature | VS Code Extension | Visual Studio Extension |
|---------|------------------|------------------------|
| Syntax Highlighting | ✅ | ✅ |
| Language Server | ✅ | 🔄 Planned |
| Code Completion | ✅ | 🔄 Planned |
| Diagnostics | ✅ | 🔄 Planned |
| Build Integration | ✅ | 🔄 Planned |
| Platforms | All | Windows only |
| Status | Stable | Initial release |

Legend: ✅ = Available, 🔄 = Planned, ❌ = Not supported

## Shared Components

Both extensions share the following components:

1. **TextMate Grammar** (`settex.tmLanguage.json`)
   - Defines syntax highlighting rules
   - Supports all Settex V2 features
   - Maintained in `vscode-settex/syntaxes/`
   - Copied to VS extension during build

2. **Language Configuration**
   - Bracket pairs and auto-closing
   - Comment styles (`#` and `//`)
   - Indentation rules
   - Maintained in `vscode-settex/language-configuration.json`

## Development

### VS Code Extension

```bash
cd vscode-settex
npm install
npm run compile
# Test in VS Code: Press F5 to launch Extension Development Host
```

See `vscode-settex/TESTING.md` for detailed testing instructions.

### Visual Studio Extension

**Prerequisites**: Windows + Visual Studio 2022 with extension development workload

```powershell
cd vs-settex/Settex.VisualStudio
# Open in Visual Studio
# Press F5 to launch experimental instance
```

See `vs-settex/BUILDING.md` for detailed build instructions.

## Contributing

Contributions to either extension are welcome! Please:

1. Fork the [Settex repository](https://github.com/74nu5/Settex)
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

### Maintainer Notes

When updating syntax highlighting:
1. Update `vscode-settex/syntaxes/settex.tmLanguage.json`
2. Copy to `vs-settex/Settex.VisualStudio/Grammars/settex.tmLanguage.json`
3. Test both extensions
4. Update version numbers in manifests

## License

Both extensions are licensed under the MIT License. See the LICENSE files in each extension directory for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/74nu5/Settex/issues)
- **Discussions**: [GitHub Discussions](https://github.com/74nu5/Settex/discussions)
- **Documentation**: [Settex README](../README.md)

## Links

- [Main Settex Repository](https://github.com/74nu5/Settex)
- [Settex Documentation](https://github.com/74nu5/Settex#readme)
- [Settex Language Specifications](../specs/)
