# Settex Visual Studio Extension

This directory contains the Visual Studio 2022+ extension for Settex.

## Overview

The Settex Visual Studio extension provides comprehensive IDE support for `.settex` configuration files, including:

- **Syntax Highlighting**: Full TextMate grammar-based syntax highlighting
- **Editor Features**: Bracket matching, auto-indentation, comment toggling
- **Build Integration**: (Planned) Automatic compilation during build
- **IntelliSense**: (Planned) Code completion and hover information via Language Server Protocol

## Project Structure

```
Settex.VisualStudio/
├── Grammars/                               # Language grammar files
│   ├── settex.tmLanguage.json             # TextMate grammar for syntax highlighting
│   └── settex-language-configuration.json  # Language configuration (brackets, comments, etc.)
├── Settex.pkgdef                           # Package definition for VS registration
├── Settex.VisualStudioPackage.cs          # Main extension package class
├── source.extension.vsixmanifest           # Extension manifest
├── Settex.VisualStudio.csproj             # Project file
├── README.md                               # User documentation
└── LICENSE.txt                             # MIT License
```

## Building

**Note**: This extension can only be built on Windows with Visual Studio 2022 or later installed.

### Prerequisites

1. **Windows 10/11**
2. **Visual Studio 2022** (any edition) with:
   - Visual Studio extension development workload
   - .NET Framework 4.7.2 targeting pack
3. **.NET SDK** (for the VSIX SDK)

### Build Steps

#### Using Visual Studio (Recommended)

1. Open `Settex.VisualStudio.slnx` in Visual Studio 2022+
2. Build the solution (Ctrl+Shift+B)
3. The `.vsix` file will be generated in `bin/Debug/` or `bin/Release/`

#### Using MSBuild (Command Line)

```powershell
# Restore packages
msbuild /t:Restore Settex.VisualStudio.csproj

# Build
msbuild /t:Build /p:Configuration=Release Settex.VisualStudio.csproj
```

#### Using dotnet CLI

**Note**: This may not work on non-Windows platforms due to .NET Framework 4.7.2 dependency.

```bash
dotnet restore Settex.VisualStudio.csproj
dotnet build Settex.VisualStudio.csproj -c Release
```

## Testing

### Installing Locally

1. Build the extension
2. Close all Visual Studio instances
3. Double-click the generated `.vsix` file in `bin/Debug/` or `bin/Release/`
4. Follow the installation wizard
5. Restart Visual Studio

### Testing in Experimental Instance

1. Open the project in Visual Studio
2. Press **F5** to debug
3. An experimental instance of Visual Studio will launch with the extension loaded
4. Create or open a `.settex` file to test syntax highlighting

### Uninstalling

1. Go to **Extensions > Manage Extensions**
2. Find "Settex Language Support"
3. Click **Uninstall**
4. Restart Visual Studio

## Features

### Current Features (v1.0.0)

- ✅ Syntax highlighting for all Settex V2 features
  - Keywords: `settings`, `env`, `include`, `let`, `for`, `in`, `if`
  - Operators: `=`, `:=`, arithmetic, comparison, logical, coalesce
  - String interpolation: `"${variable}"`
  - Comments: `#` and `//`
  - Constants: `true`, `false`, `null`
- ✅ Bracket matching and auto-closing
- ✅ Auto-indentation for nested blocks
- ✅ Comment toggling support
- ✅ File extension association (`.settex`)

### Planned Features

- 🔄 Language Server Protocol integration
  - Code completion
  - Hover information
  - Go to definition
  - Find references
- 🔄 Build integration
  - Automatic compilation during build
  - Error diagnostics in Error List
  - Quick fixes
- 🔄 Code snippets
- 🔄 Refactoring support

## Architecture

### Technology Stack

- **SDK**: `CodingWithCalvin.VsixSdk/1.0.0` - Modern SDK-style VSIX project
- **Target Framework**: .NET Framework 4.7.2
- **VS SDK**: Microsoft.VisualStudio.SDK 17.*
- **Build Tools**: Microsoft.VSSDK.BuildTools 17.*

### Key Components

1. **SettexVisualStudioPackage**: Main package class implementing `AsyncPackage`
   - Handles extension initialization
   - Provides entry point for VS integration

2. **Settex.pkgdef**: Package definition file
   - Registers `.settex` file extension
   - Configures TextMate grammar location
   - Sets up language service

3. **TextMate Grammar**: `settex.tmLanguage.json`
   - Defines syntax highlighting rules
   - Shared with VS Code extension
   - Supports all Settex V2 features

4. **Language Configuration**: `settex-language-configuration.json`
   - Bracket pairs and auto-closing
   - Comment styles
   - Indentation rules

### Extension Registration

The extension registers itself with Visual Studio through:

1. **File Extension**: Associates `.settex` files with the Settex language
2. **Language Service**: Registers the Settex language service
3. **TextMate Repository**: Points to the grammar file for syntax highlighting
4. **Editor Factory**: Configures the Settex editor

## Publishing

### To Visual Studio Marketplace

1. Build the extension in Release configuration
2. Sign up for a Visual Studio Marketplace publisher account
3. Create a new extension entry
4. Upload the `.vsix` file
5. Fill in marketplace metadata (description, screenshots, etc.)
6. Submit for publication

### Manual Distribution

The `.vsix` file can be distributed directly:
- Share the file for manual installation
- Host on GitHub Releases
- Include in documentation for offline installation

## Troubleshooting

### Build Errors

**Problem**: "Unable to find a project to restore"
- **Solution**: This is expected on non-Windows platforms. VSIX extensions require Windows + Visual Studio.

**Problem**: "Could not load file or assembly Microsoft.VisualStudio.*"
- **Solution**: Install Visual Studio 2022 with the "Visual Studio extension development" workload.

**Problem**: Package version conflicts
- **Solution**: Update `Microsoft.VisualStudio.SDK` and `Microsoft.VSSDK.BuildTools` to the latest 17.* versions.

### Runtime Issues

**Problem**: Extension doesn't load in Visual Studio
- **Solution**: Check the ActivityLog.xml in `%AppData%\Microsoft\VisualStudio\17.0_<hash>\ActivityLog.xml`

**Problem**: Syntax highlighting not working
- **Solution**: Verify the TextMate grammar file is included in the VSIX (check with 7-Zip or similar)

**Problem**: File extension not recognized
- **Solution**: Ensure the `.pkgdef` file is properly registered in the VSIX manifest

## Contributing

Contributions are welcome! See the main [Settex repository](https://github.com/74nu5/Settex) for contribution guidelines.

### Development Workflow

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test in Visual Studio experimental instance
5. Submit a pull request

### Code Style

- Follow C# coding conventions
- Use async/await for VS package initialization
- Keep the package initialization lightweight
- Document public APIs

## Resources

- [Visual Studio Extensibility Documentation](https://learn.microsoft.com/en-us/visualstudio/extensibility/)
- [VSIX Cookbook](https://www.vsixcookbook.com/)
- [CodingWithCalvin VSIX SDK](https://github.com/CodingWithCalvin/VsixSdk)
- [Settex Main Repository](https://github.com/74nu5/Settex)

## License

MIT License - see [LICENSE.txt](LICENSE.txt) for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/74nu5/Settex/issues)
- **Discussions**: [GitHub Discussions](https://github.com/74nu5/Settex/discussions)
- **Email**: Contact the repository owner

---

**Note**: This extension is built with the modern CodingWithCalvin.VsixSdk, allowing SDK-style project format. However, it still requires Windows and .NET Framework 4.7.2 due to Visual Studio extension platform requirements.
