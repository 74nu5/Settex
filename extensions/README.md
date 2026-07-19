# Settex Editor Extensions

This directory contains editor extensions for Settex, providing IDE support for `.settex` configuration files.

📖 **Documentation:** [settex.74nu5.dev](https://settex.74nu5.dev)

> **Language-server requirement:** both extensions run the Settex language server (hover, go-to-definition, find-references, diagnostics, completion) on the **.NET 10 runtime** (LTS).
>
> - **VS Code** acquires it for you: the extension depends on Microsoft's [.NET Install Tool](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.vscode-dotnet-runtime), installed automatically alongside Settex, which fetches a private user-local copy on first use — no admin rights, nothing extra in the VSIX.
> - **Visual Studio** users generally already have it via a .NET workload; otherwise install it from the Visual Studio Installer (component *.NET 10.0 Runtime*) or from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0).
>
> In every case, if the runtime can't be found, syntax highlighting and snippets keep working and the extension shows an actionable message with a download link.

## Available Extensions

### 1. Visual Studio Code Extension

**Location**: `vscode-settex/`

A VS Code extension providing syntax highlighting, snippets, and language-server support (IntelliSense, diagnostics, hover, navigation) for Settex.

**Features**:
- ✅ Syntax highlighting
- ✅ Language server integration
- ✅ Code completion / IntelliSense
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
- ✅ Code snippets (11 built-in snippets)
- ✅ Language Server integration (IntelliSense)
- ✅ Build integration (manual + automatic on save)
- ✅ Options page for configuration
- 🔄 MSBuild integration (via Settex.Build package)

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
| Language Server | ✅ | ✅ |
| Code Completion | ✅ | ✅ |
| Diagnostics | ✅ | ✅ |
| Code Snippets | ✅ | ✅ |
| Build Integration | ✅ | ✅ (Manual + Auto) |
| Auto-Compile on Save | ❌ | ✅ |
| Options/Settings | ❌ | ✅ |
| Platforms | All | Windows only |
| Status | Stable | Latest (v1.2.0) |

Legend: ✅ = Available, 🔄 = Planned, ❌ = Not supported

## Shared Components

Both extensions share the following components:

1. **TextMate Grammar** (`settex.tmLanguage.json`)
   - Defines syntax highlighting rules
   - Supports all Settex V2 features
   - Maintained in `vscode-settex/syntaxes/`
   - Manually copied to VS extension at `vs-settex/Settex.VisualStudio/Grammars/`
   - **Important**: Keep both copies in sync when updating the grammar

2. **Language Configuration**
   - Bracket pairs and auto-closing
   - Comment styles (`#` and `//`)
   - Indentation rules
   - Maintained in `vscode-settex/language-configuration.json`
   - Manually copied to VS extension at `vs-settex/Settex.VisualStudio/Grammars/settex-language-configuration.json`
   - **Important**: Keep both copies in sync when updating the configuration

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
