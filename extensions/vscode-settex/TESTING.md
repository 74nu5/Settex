# Settex VS Code Extension - Testing Guide

## Prerequisites

1. **VS Code** installed
2. **.NET 10 SDK** installed
3. **Node.js** and **npm** installed

## Build Steps

### 1. Build the Language Server

```bash
cd D:\Perso\Settex
dotnet build src\Settex.LanguageServer\Settex.LanguageServer.csproj --configuration Debug
```

### 2. Build the VS Code Extension

```bash
cd D:\Perso\Settex\extensions\vscode-settex
npm install
npm run compile
```

## Testing the Extension

### Method 1: Using F5 (Recommended)

1. Open VS Code in the extension directory:
   ```bash
   code D:\Perso\Settex\extensions\vscode-settex
   ```

2. Press **F5** to launch Extension Development Host

3. In the new window, open a `.settex` file:
   - Open folder: `D:\Perso\Settex`
   - Open file: `extensions\vscode-settex\test.settex`

### Method 2: Using Workspace File

1. Open the workspace:
   ```bash
   code D:\Perso\Settex\extensions\vscode-settex\vscode-settex.code-workspace
   ```

2. Press **F5**

3. Open a `.settex` file in the new window

## What to Expect

### ✅ Working Features

1. **Syntax Highlighting**
   - Keywords: `settings`, `env`, `let`, `for`, `include`, `if`
   - Operators: `=`, `:=`, `==`, `!=`, `and`, `or`, etc.
   - Strings with interpolation: `"Hello ${name}"`
   - Comments: `#` and `//`
   - Numbers: integers and floats

2. **Code Snippets**
   - Type `settings` + Tab → Full settings block
   - Type `env` + Tab → Environment block
   - Type `let` + Tab → Variable declaration
   - Type `for` + Tab → For loop
   - Type `if` + Tab → If expression
   - And 7 more snippets!

3. **Diagnostics (Errors/Warnings)**
   - **Lexer errors**: Invalid characters, unclosed strings, etc.
   - **Parser errors**: Missing tokens, unexpected syntax
   - **Real-time updates**: Diagnostics appear as you type

### Example with Error

Open `test.settex` - it contains an intentional error (unclosed string on line 6):

```settex
settings {
    App.Name = "TestApp     # ← Missing closing quote
    App.Url = apiUrl
}
```

You should see:
- 🔴 Red squiggly underline
- Error message in Problems panel
- Error code: STX101 (Lexer error)

### Debugging

If the extension doesn't work:

1. **Check Language Server logs**:
   - Open "Output" panel in VS Code
   - Select "Settex Language Server" from dropdown

2. **Check Extension Development Host console**:
   - In Extension Development Host: Help → Toggle Developer Tools
   - Check Console tab for errors

3. **Verify server is running**:
   ```powershell
   # Should show Settex.LanguageServer.dll
   Get-Process dotnet | Select-Object Id, ProcessName, StartTime
   ```

## Current Limitations

- ❌ No IntelliSense yet (Phase 4)
- ❌ No Hover documentation (Phase 5)
- ❌ No Go to Definition (Phase 6)
- ❌ No Formatting (Phase 7)

These features will be added in future phases!

## Troubleshooting

### Extension doesn't activate

- Check that `activationEvents: ["*"]` is in `package.json`
- Verify the workspace folder is `vscode-settex`, not parent directory

### Language Server fails to start

- Build the server: `dotnet build src\Settex.LanguageServer\Settex.LanguageServer.csproj`
- Check path in `extension.ts` points to the correct DLL
- Verify .NET 10 SDK is installed: `dotnet --version`

### No diagnostics appear

- Check Output panel for server logs
- Verify file has `.settex` extension
- Try closing and reopening the file

## Next Steps

To continue V3 development:
- **Phase 4**: Add IntelliSense (keyword completion, variable completion)
- **Phase 5**: Add Hover tooltips (variable info, keyword docs)
- **Phase 6**: Add Navigation (go to definition, find references)
- **Phase 7**: Add Formatting and code actions
