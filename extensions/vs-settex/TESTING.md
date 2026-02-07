# Testing Guide - Settex Visual Studio Extension

This guide provides instructions for testing the Settex Visual Studio extension.

## Prerequisites

- **Windows 10/11**
- **Visual Studio 2022 or later** with:
  - Visual Studio extension development workload
  - .NET Framework 4.7.2 targeting pack

## Quick Test (For Developers)

### 1. Build and Run in Experimental Instance

The easiest way to test the extension is using Visual Studio's experimental instance:

1. **Open the project**:
   ```
   Open Settex.VisualStudio.slnx in Visual Studio 2022+
   ```

2. **Press F5** or click Debug > Start Debugging

3. **Visual Studio Experimental Instance will launch**:
   - This is a separate instance with the extension loaded
   - Your main VS installation remains unaffected

4. **Test the extension**:
   - Create a new file: File > New > File
   - Save it with `.settex` extension (e.g., `test.settex`)
   - Paste the example code below
   - Verify syntax highlighting works

### 2. Test Example

Paste this code into your test `.settex` file:

```settex
# Settex test file
let baseUrl = "http://localhost"
let port = 8000

settings {
  ApplicationName = "MyApp"
  Version = "1.0.0"
  
  Server {
    Host = baseUrl
    Port = port
    Url = "${baseUrl}:${port}"
    MaxConnections := 100
  }
  
  Features = [
    "auth"
    "api"
    "web"
  ]
  
  Logging {
    LogLevel {
      Default = "Information"
      Microsoft = "Warning"
    }
  }
}

env Development {
  let port = 5000
  
  settings {
    Server {
      Port = port
      Url = "${baseUrl}:${port}"
    }
    
    Logging.LogLevel.Default = "Debug"
  }
}

env Production {
  let baseUrl = "https://api.example.com"
  let port = 443
  
  settings {
    Server {
      Port = port
      Url = baseUrl
    }
    
    Logging.LogLevel.Default = "Warning"
  }
}

// For loop example
let services = ["auth", "api", "notifications"]

settings {
  Services = [
    for service in services {
      item {
        Name = service
        Url = "/api/${service}"
        Enabled = true if env == "Production"
      }
    }
  ]
}
```

### 3. What to Verify

Check that the following are highlighted correctly:

- ✅ **Keywords** (blue): `settings`, `env`, `let`, `for`, `in`, `if`
- ✅ **Operators** (red/purple): `=`, `:=`, operators in expressions
- ✅ **Strings** (brown/orange): `"http://localhost"`, `"MyApp"`, etc.
- ✅ **String interpolation** (yellow/cyan): `"${baseUrl}:${port}"`
- ✅ **Numbers** (green): `8000`, `5000`, `443`, `100`
- ✅ **Booleans** (blue): `true`, `false`
- ✅ **Comments** (green/gray): `#` and `//` comments
- ✅ **Brackets**: Matching pairs highlight when cursor is on them
- ✅ **Auto-indentation**: Press Enter after `{` - next line indents

### 4. Test Editor Features

- **Bracket Matching**: Click on `{` or `}` - matching bracket should highlight
- **Auto-closing**: Type `{` - it should automatically add `}`
- **Comment Toggle**: 
  - Select a line
  - Press `Ctrl+/` or `Ctrl+K, Ctrl+C`
  - Line should get `#` comment prefix
- **Indentation**:
  - Type `settings {` and press Enter
  - Next line should be indented automatically

## Installing Built Extension

### 1. Build the Extension

```powershell
# In Visual Studio
Build > Build Solution (Ctrl+Shift+B)

# Or via MSBuild
msbuild /t:Build /p:Configuration=Release Settex.VisualStudio.csproj
```

The `.vsix` file will be in `bin/Debug/` or `bin/Release/`.

### 2. Install the VSIX

1. **Close all Visual Studio instances**
2. **Locate the .vsix file**: `bin/Release/Settex.VisualStudio.vsix`
3. **Double-click the .vsix file**
4. **Follow the installation wizard**:
   - Select Visual Studio editions to install to
   - Click "Install"
5. **Restart Visual Studio**

### 3. Verify Installation

1. Open Visual Studio
2. Go to **Extensions > Manage Extensions**
3. Search for "Settex" in "Installed" tab
4. You should see "Settex Language Support"

### 4. Test in Normal Instance

1. Create or open a `.settex` file
2. Verify syntax highlighting and editor features work

## Troubleshooting

### Experimental Instance Issues

**Problem**: Experimental instance won't start
- **Solution**: Clean and rebuild the solution

**Problem**: Extension not loading in experimental instance
- **Solution**: Check Output window > "Debug" for errors

### Installation Issues

**Problem**: "This extension is not installable on any currently installed products"
- **Solution**: Verify you have Visual Studio 2022 or later (version 17.0+)

**Problem**: Extension installs but doesn't work
- **Solution**: 
  1. Close Visual Studio
  2. Delete: `%LocalAppData%\Microsoft\VisualStudio\17.0_<hash>\Extensions\`
  3. Reinstall the .vsix

### Syntax Highlighting Not Working

**Problem**: No syntax highlighting for .settex files
- **Solution**: 
  1. Check file extension is exactly `.settex`
  2. Close and reopen the file
  3. Restart Visual Studio

**Problem**: Partial highlighting (some features work, others don't)
- **Solution**: 
  1. Check the TextMate grammar is included in the VSIX (extract .vsix with 7-Zip)
  2. Verify `Settex.pkgdef` is included
  3. Check ActivityLog.xml for errors: `%AppData%\Microsoft\VisualStudio\17.0_<hash>\ActivityLog.xml`

## Advanced Testing

### Debugging the Extension

1. Set breakpoints in `SettexVisualStudioPackage.cs`
2. Press F5 to start debugging
3. Open a .settex file in the experimental instance
4. Breakpoints in `InitializeAsync` should hit

### Inspecting Extension Registration

1. Install the extension
2. Open Registry Editor (regedit)
3. Navigate to: `HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\17.0_<hash>\Languages`
4. Verify "Settex" language is registered

### Checking Package Load

1. Open Visual Studio
2. Go to Help > About Microsoft Visual Studio
3. Click "Copy Info"
4. Search for "Settex" in the copied text
5. Extension should be listed

## Performance Testing

### File Size Test

Test with large `.settex` files (1000+ lines) to ensure syntax highlighting performs well.

### Startup Time Test

Measure Visual Studio startup time before and after installing the extension to ensure minimal impact.

## Automated Testing (Future)

Currently, the extension has no automated tests. Future versions may include:
- Unit tests for package initialization
- Integration tests for syntax highlighting
- Performance benchmarks

## Reporting Issues

If you find issues:

1. **Collect Information**:
   - Visual Studio version
   - Extension version
   - Steps to reproduce
   - ActivityLog.xml (if applicable)

2. **Create an Issue**:
   - Go to: https://github.com/74nu5/Settex/issues
   - Click "New Issue"
   - Provide detailed description with reproduction steps
   - Attach relevant files/logs

## Additional Resources

- [Visual Studio Extensibility Documentation](https://learn.microsoft.com/en-us/visualstudio/extensibility/)
- [Debugging Extensions](https://learn.microsoft.com/en-us/visualstudio/extensibility/debugger/debugging-visual-studio-extensions)
- [VSIX Troubleshooting](https://learn.microsoft.com/en-us/visualstudio/extensibility/troubleshooting-vsix-package)

---

**Happy Testing!** 🚀
