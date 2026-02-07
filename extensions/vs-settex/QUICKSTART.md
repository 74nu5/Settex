# Quick Start Guide - Settex Visual Studio Extension

This guide helps you quickly get started with building and testing the Settex Visual Studio extension on Windows.

## For Windows Users with Visual Studio 2022+

### Prerequisites Checklist

Before you begin, ensure you have:

- [ ] **Windows 10 or 11**
- [ ] **Visual Studio 2022 or later** (any edition: Community, Professional, or Enterprise)
- [ ] **Visual Studio extension development workload** installed
  - If not installed, run Visual Studio Installer and add "Visual Studio extension development"

### Step 1: Clone the Repository

```powershell
git clone https://github.com/74nu5/Settex.git
cd Settex
```

### Step 2: Open in Visual Studio

```powershell
cd extensions\vs-settex\Settex.VisualStudio
start Settex.VisualStudio.slnx
```

This will open the extension project in Visual Studio.

### Step 3: Build the Extension

In Visual Studio:
1. Press **Ctrl+Shift+B** (or Build > Build Solution)
2. Wait for the build to complete
3. Check the Output window for any errors

### Step 4: Test in Experimental Instance

1. Press **F5** (or Debug > Start Debugging)
2. Visual Studio will launch a second instance (Experimental Instance)
3. In the experimental instance:
   - File > New > File
   - Save as `test.settex`
   - Paste the example code (see below)
   - Verify syntax highlighting works!

### Example Code to Test

```settex
# This is a Settex configuration file
let baseUrl = "http://localhost"
let port = 8000

settings {
  ApplicationName = "MyApp"
  
  Server {
    Host = baseUrl
    Port = port
    Url = "${baseUrl}:${port}"
  }
  
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

### Step 5: Verify Features

Check that:
- ✅ Keywords (`settings`, `env`, `let`) are highlighted
- ✅ Strings are highlighted in color
- ✅ String interpolation `${...}` works
- ✅ Comments (`#` and `//`) are highlighted
- ✅ Brackets match when you click on them
- ✅ Auto-indentation works when you press Enter after `{`

### Step 6: Build VSIX Package

To create an installable `.vsix` file:

1. In Visual Studio, select **Release** configuration (toolbar dropdown)
2. Press **Ctrl+Shift+B** to build
3. The VSIX file will be created at:
   ```
   bin\Release\Settex.VisualStudio.vsix
   ```

### Step 7: Install the Extension (Optional)

To install in your main Visual Studio:

1. **Close all Visual Studio instances**
2. **Double-click** `bin\Release\Settex.VisualStudio.vsix`
3. **Follow the installation wizard**
4. **Restart Visual Studio**
5. **Verify**: Extensions > Manage Extensions > Search "Settex"

## Troubleshooting

### Build Fails

**Error**: "Could not load file or assembly Microsoft.VisualStudio.*"
- **Fix**: Install "Visual Studio extension development" workload via Visual Studio Installer

**Error**: "The command ... exited with code 1"
- **Fix**: Clean solution (Build > Clean Solution), then rebuild

### Experimental Instance Won't Start

- **Fix 1**: Reset experimental instance:
  ```powershell
  "%ProgramFiles%\Microsoft Visual Studio\2022\Community\VSSDK\VisualStudioIntegration\Tools\Bin\CreateExpInstance.exe" /Reset /VSInstance=17.0 /RootSuffix=Exp
  ```
- **Fix 2**: Close all VS instances and try again

### No Syntax Highlighting

- **Check**: File extension is exactly `.settex` (not `.txt` or `.settex.txt`)
- **Fix**: Close and reopen the file
- **Fix**: Restart Visual Studio

## Command Line Build (Alternative)

If you prefer command line:

```powershell
# Restore packages
msbuild /t:Restore Settex.VisualStudio.csproj

# Build
msbuild /t:Build /p:Configuration=Release Settex.VisualStudio.csproj
```

## Next Steps

- 📖 Read the full [BUILDING.md](BUILDING.md) for detailed information
- 🧪 See [TESTING.md](TESTING.md) for comprehensive testing guide
- 📚 Check the main [README.md](README.md) for feature documentation
- 🐛 Report issues at: https://github.com/74nu5/Settex/issues

## Support

- **Issues**: https://github.com/74nu5/Settex/issues
- **Discussions**: https://github.com/74nu5/Settex/discussions
- **Main Repo**: https://github.com/74nu5/Settex

---

**Note**: This extension only builds and runs on Windows with Visual Studio 2022+. It cannot be built on macOS or Linux due to .NET Framework 4.7.2 and Visual Studio SDK requirements.

Happy coding! 🎉
