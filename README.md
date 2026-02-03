# Settex

**Settex** is a human-friendly configuration language for .NET that compiles to `appsettings.json` files. It provides a clean, concise syntax with support for environment-specific overlays and deep merging.

## ✨ Features

- 🎯 **Simple, intuitive syntax** - No complex markup, just clean configuration
- 🔀 **Environment overlays** - Define base settings once, override per environment
- 🔄 **Deep merging** - Objects merge intelligently, arrays replace completely
- 🛠️ **MSBuild integration** - Automatic compilation during build
- 🖥️ **CLI tool** - Standalone compiler with beautiful diagnostics
- 📍 **Precise diagnostics** - Line/column error reporting with IDE integration
- ✅ **Conditional writes** - Only updates files when content changes

## 📦 Installation

### MSBuild Integration (Recommended)

Add the Settex.Build package to your project:

```bash
dotnet add package Settex.Build
```

Create a `.settex` file in your project root, and it will be automatically compiled during build.

### CLI Tool

Install as a global .NET tool:

```bash
dotnet tool install --global Settex.Cli
```

Then compile files manually:

```bash
settex build appsettings.settex
settex build config.settex -o ./output
```

## 🚀 Quick Start

Create a file named `appsettings.settex`:

```settex
settings {
  ApplicationName = "MyApp"
  
  Logging {
    LogLevel {
      Default = "Information"
      Microsoft = "Warning"
    }
  }
  
  AllowedHosts = ["localhost", "*.example.com"]
  
  ConnectionStrings {
    DefaultConnection = "Server=localhost;Database=MyDb"
  }
}

env Development {
  Logging.LogLevel.Default = "Debug"
  ConnectionStrings.DefaultConnection = "Server=localhost;Database=MyDb_Dev"
}

env Production {
  Logging.LogLevel.Default = "Warning"
  ConnectionStrings.DefaultConnection = "Server=prod-server;Database=MyDb"
}
```

Build your project, and Settex will generate:
- `appsettings.json` - Base settings
- `appsettings.Development.json` - Base merged with Development overlay
- `appsettings.Production.json` - Base merged with Production overlay

## 📖 Language Syntax

### Settings Block

The `settings` block defines your base configuration:

```settex
settings {
  AppName = "MyApp"
  Version = "1.0.0"
  EnableFeatureX = true
}
```

### Nested Objects

Use nested blocks for hierarchical configuration:

```settex
settings {
  Database {
    Host = "localhost"
    Port = 5432
    Options {
      Timeout = 30
      Retry = true
    }
  }
}
```

### Arrays

Define arrays with comma-separated values (commas are optional):

```settex
settings {
  AllowedHosts = [
    "localhost"
    "*.example.com"
    "api.production.com"
  ]
  
  Ports = [8080, 8081, 8082]
}
```

### Dot-Path Assignments

Set nested values using dot notation:

```settex
settings {
  Logging.LogLevel.Default = "Information"
  Logging.LogLevel.Microsoft = "Warning"
  Logging.LogLevel.System = "Error"
}
```

### Tagged Objects

Create objects with a tag (key):

```settex
settings {
  Providers {
    Provider "AzureAd" {
      ClientId = "abc-123"
      Authority = "https://login.microsoftonline.com"
    }
    
    Provider "Google" {
      ClientId = "xyz-456"
      ClientSecret = "secret"
    }
  }
}
```

Generates:

```json
{
  "Providers": {
    "AzureAd": {
      "ClientId": "abc-123",
      "Authority": "https://login.microsoftonline.com"
    },
    "Google": {
      "ClientId": "xyz-456",
      "ClientSecret": "secret"
    }
  }
}
```

### Environment Overlays

Define environment-specific overrides with `env` blocks:

```settex
settings {
  ApiUrl = "https://api.example.com"
  LogLevel = "Information"
}

env Development {
  ApiUrl = "http://localhost:5000"
  LogLevel = "Debug"
}

env Staging {
  ApiUrl = "https://staging-api.example.com"
  LogLevel = "Debug"
}

env Production {
  LogLevel = "Warning"
}
```

Each `env` block generates a separate `appsettings.{Environment}.json` file with the base settings merged with the overlay.

### Merging Behavior

- **Objects**: Merged deeply (properties combined)
- **Arrays**: Replaced completely (no merging)
- **Primitives**: Replaced

Example:

```settex
settings {
  Database {
    Host = "localhost"
    Port = 5432
  }
  Tags = ["dev", "test"]
}

env Production {
  Database.Host = "prod-server"  // Only Host changes
  Tags = ["prod"]  // Array replaced entirely
}
```

Production result:
```json
{
  "Database": {
    "Host": "prod-server",
    "Port": 5432
  },
  "Tags": ["prod"]
}
```

## 🔧 MSBuild Configuration

The Settex.Build package automatically discovers `*.settex` files in your project. You can customize the behavior:

```xml
<PropertyGroup>
  <!-- Change output directory (default: project root) -->
  <SettexOutputDirectory>$(MSBuildProjectDirectory)\config</SettexOutputDirectory>
  
  <!-- Disable automatic compilation -->
  <EnableSettexCompilation>false</EnableSettexCompilation>
</PropertyGroup>

<!-- Or specify files explicitly -->
<ItemGroup>
  <SettexFile Include="config\app.settex" />
  <SettexFile Include="config\services.settex" />
</ItemGroup>
```

## 🛠️ CLI Usage

```bash
# Compile a file
settex build appsettings.settex

# Specify output directory
settex build config.settex -o ./output

# Show help
settex --help
settex build --help
```

The CLI provides:
- ✨ Beautiful formatted diagnostics with Spectre.Console
- 📊 Progress indicators
- 🎨 Color-coded error messages
- 📍 Precise error locations

## 📝 Value Types

Settex supports these value types:

- **Strings**: `"Hello World"` or `'Single quotes'`
- **Numbers**: `42`, `3.14`, `-10`
- **Booleans**: `true`, `false`
- **Null**: `null`
- **Arrays**: `[1, 2, 3]` or multiline
- **Objects**: Nested blocks

## ⚠️ Error Handling

Settex provides precise error diagnostics:

```
appsettings.settex(12,5): error: Expected '=' after path
  LogLevel.Default "Information"
      ^
```

Errors include:
- File name and location (line, column)
- Clear error message
- Source line with error indicator
- IDE integration (clickable in Visual Studio, VS Code, Rider)

## 🏗️ Project Structure

```
MyProject/
├── appsettings.settex          # Your configuration source
├── appsettings.json            # Generated (base)
├── appsettings.Development.json # Generated (base + dev)
├── appsettings.Production.json  # Generated (base + prod)
├── MyProject.csproj
└── Program.cs
```

Add `*.settex` to source control, and let `.gitignore` handle the generated JSON files (or commit them for deployment).

## 📚 Examples

### ASP.NET Core Web API

```settex
settings {
  Logging {
    LogLevel {
      Default = "Information"
      Microsoft.AspNetCore = "Warning"
    }
  }
  
  AllowedHosts = "*"
  
  ConnectionStrings {
    DefaultConnection = "Server=localhost;Database=MyApi"
  }
  
  JwtSettings {
    SecretKey = "dev-secret-key-change-in-production"
    Issuer = "MyApi"
    Audience = "MyApiClients"
    ExpirationMinutes = 60
  }
}

env Development {
  Logging.LogLevel.Default = "Debug"
  ConnectionStrings.DefaultConnection = "Server=localhost;Database=MyApi_Dev"
  JwtSettings.ExpirationMinutes = 1440
}

env Production {
  Logging.LogLevel.Default = "Warning"
  Logging.LogLevel.Microsoft.AspNetCore = "Error"
  ConnectionStrings.DefaultConnection = "${DB_CONNECTION_STRING}"
  JwtSettings.SecretKey = "${JWT_SECRET}"
}
```

### Multi-Service Configuration

```settex
settings {
  Services {
    Service "EmailService" {
      Enabled = true
      Provider = "SendGrid"
      ApiKey = "dev-key"
    }
    
    Service "StorageService" {
      Enabled = true
      Provider = "LocalDisk"
      RootPath = "./storage"
    }
    
    Service "CacheService" {
      Enabled = false
      Provider = "Memory"
    }
  }
}

env Production {
  Services {
    Service "EmailService" {
      ApiKey = "${SENDGRID_KEY}"
    }
    
    Service "StorageService" {
      Provider = "AzureBlobStorage"
      ConnectionString = "${AZURE_STORAGE}"
    }
    
    Service "CacheService" {
      Enabled = true
      Provider = "Redis"
      ConnectionString = "${REDIS_URL}"
    }
  }
}
```

## 🎯 Design Goals

1. **Readability**: Configuration should be easy to read and understand
2. **DRY Principle**: Define once, override only what changes
3. **Safety**: Strong typing and validation with clear error messages
4. **Integration**: Seamless .NET build integration
5. **Tooling**: Great developer experience with CLI and IDE support

## 📄 License

MIT License - see LICENSE file for details

## 🤝 Contributing

Contributions welcome! Please open an issue or pull request.

## 🔗 Links

- [GitHub Repository](https://github.com/settex/settex)
- [NuGet Package (Build)](https://nuget.org/packages/Settex.Build)
- [NuGet Package (CLI)](https://nuget.org/packages/Settex.Cli)
