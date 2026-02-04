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

### V2 Features 🆕

- 📂 **File includes** - Reuse configuration across multiple files
- 🔤 **Variables (`let`)** - Define reusable values with proper scoping
- 🧮 **Expressions** - Arithmetic, logical, comparison, and null coalescing
- 💬 **String interpolation** - Embed variables and expressions in strings: `"${host}:${port}"`
- ❓ **Conditional assignments** - `Value = expr if condition` for environment-specific values
- ⚙️ **Set-if-missing operator** - `Port := 8080` only sets if not already defined
- 🔁 **For loops** - Generate array elements dynamically from collections

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

## 🆕 V2 Features

### File Includes

Reuse configuration across multiple files with `include`:

```settex
# common.settex
let host = "localhost"
let defaultPort = 8000

settings {
  Server {
    Host = host
    Port = defaultPort
  }
}

# appsettings.settex
include "common.settex"

settings {
  ApplicationName = "MyApp"
}

env Production {
  let host = "api.example.com"
  let defaultPort = 443
}
```

**Features**:
- Relative paths resolved from file location
- Circular dependency detection
- Variables from included files are available in current scope

### Variables with `let`

Define reusable values with proper scoping:

```settex
let basePort = 8000
let host = "localhost"
let version = "1.0.0"

settings {
  BaseUrl = "http://${host}:${basePort}"
  Version = version
}

env Development {
  let basePort = 5000  # Shadows global variable
  # host is still "localhost" from global scope
}

env Production {
  let host = "api.example.com"
  let basePort = 443
}
```

**Scoping rules**:
- Global scope: Variables defined at file level
- Environment scope: Variables in `env` blocks shadow global variables
- For loop scope: Iterator variables only exist within the loop

### Expressions

Perform calculations and logic in your configuration:

```settex
let timeout = 30
let maxRetries = 3
let enabled = true

settings {
  # Arithmetic
  TotalTimeout = timeout * maxRetries
  Port = 8000 + 80
  
  # Comparison
  IsProduction = env == "Production"
  IsLargeTimeout = timeout > 60
  
  # Logical
  ShouldCache = enabled and timeout > 10
  DebugMode = not enabled or env == "Development"
  
  # Null coalescing
  LogLevel = null ?? "Information"
  Host = null ?? "localhost"
}
```

**Supported operators**:
- Arithmetic: `+`, `-`, `*`, `/`
- Comparison: `==`, `!=`, `<`, `<=`, `>`, `>=`
- Logical: `and`, `or`, `not`
- Null coalescing: `??`

### String Interpolation

Embed variables and expressions in strings:

```settex
let host = "localhost"
let port = 8000
let protocol = "https"

settings {
  BaseUrl = "${protocol}://${host}:${port}"
  ApiEndpoint = "${protocol}://${host}:${port}/api/v1"
  Message = "Server running on port ${port + 100}"
}
```

**Note**: Interpolating `null` throws an error. Use null coalescing: `"${value ?? "default"}"`

### Conditional Assignments

Set values based on conditions:

```settex
settings {
  # Simple condition
  LogLevel = "Debug" if env == "Development"
  LogLevel = "Warning" if env == "Production"
  
  # With expressions
  MaxConnections = 100 if env == "Development"
  MaxConnections = 1000 if env == "Production"
  
  # Works in environment blocks too
  env Staging {
    CachingEnabled = true if env == "Staging"
  }
}
```

**Rules**:
- Condition must evaluate to `bool`
- Multiple conditions can assign different values
- Last matching condition wins

### Set-If-Missing Operator (`:=`)

Set a value only if not already defined:

```settex
settings {
  Server {
    Port := 8080  # Sets default
    MaxConnections := 100  # Sets default
  }
}

env Development {
  Server {
    Port = 5000  # Overrides with =
    MaxConnections := 50  # Won't set (already defined in base)
  }
}

env Production {
  Server {
    Port := 443  # Won't set (already defined in base)
    MaxConnections = 1000  # Overrides with =
  }
}
```

**Rules**:
- `:=` only sets if path doesn't exist yet
- In environment overlays, checks base settings first
- Useful for providing defaults that environments can override

### For Loops

Generate array elements dynamically:

```settex
let services = ["auth", "api", "web"]
let ports = [8001, 8002, 8003]
let host = "localhost"

settings {
  # Simple for loop
  ServiceNames = [
    for service in services {
      item { Name = service }
    }
  ]
  
  # With interpolation
  ServiceUrls = [
    for port in ports {
      item { Url = "http://${host}:${port}" }
    }
  ]
  
  # Mixed with regular elements
  Endpoints = [
    { Name = "health" Url = "/health" }
    for service in services {
      item {
        Name = service
        Url = "/api/${service}"
      }
    }
  ]
}
```

**Features**:
- Iterator variable scoped to loop body
- Can access outer scope variables
- Loop body must contain exactly one nested block (`item { ... }`)
- Nestable (for loops can contain for loops)

### Complete V2 Example

Here's a real-world example using all V2 features:

```settex
# common.settex
let baseHost = "localhost"
let basePort = 8000
let services = ["auth", "api", "notifications"]

settings {
  ApplicationName = "MyApp"
  Version = "2.0.0"
  
  Server {
    Host := baseHost
    Port := basePort
    MaxConnections := 100
  }
}

# appsettings.settex
include "common.settex"

let apiVersion = "v1"

settings {
  BaseUrl = "http://${baseHost}:${basePort}"
  
  Services = [
    for service in services {
      item {
        Name = service
        Url = "http://${baseHost}:${basePort}/${service}"
        Enabled = true if env == "Production"
        Enabled = true if env == "Development"
      }
    }
  ]
  
  Logging {
    LogLevel {
      Default = "Information" if env == "Production"
      Default = "Debug" if env == "Development"
    }
  }
}

env Development {
  let basePort = 5000
  
  settings {
    Server {
      Port = basePort
    }
    BaseUrl = "http://dev.${baseHost}:${basePort}"
  }
}

env Production {
  let baseHost = "api.example.com"
  let basePort = 443
  
  settings {
    Server {
      Port = basePort
      MaxConnections = 1000
    }
    BaseUrl = "https://${baseHost}"
  }
}
```

For more examples, see the `samples/` directory.

## 🔄 Migration Guide: V1 → V2

V2 is fully backward compatible with V1. All V1 syntax continues to work. Here's how to adopt V2 features incrementally:

### Step 1: Extract Common Values

**V1:**
```settex
settings {
  Server.Host = "localhost"
  Server.Port = 8080
  Database.Host = "localhost"
  Database.Port = 5432
}

env Production {
  Server.Host = "api.example.com"
  Database.Host = "db.example.com"
}
```

**V2:**
```settex
let defaultHost = "localhost"

settings {
  Server.Host = defaultHost
  Server.Port = 8080
  Database.Host = defaultHost
  Database.Port = 5432
}

env Production {
  let defaultHost = "api.example.com"
  Database.Host = "db.example.com"
}
```

### Step 2: Use Set-If-Missing for Defaults

**V1:**
```settex
settings {
  Server.Port = 8080
}

env Development {
  Server.Port = 8080  # Repeated
}

env Production {
  Server.Port = 443
}
```

**V2:**
```settex
settings {
  Server.Port := 8080  # Default
}

env Development {
  # Inherits default 8080
}

env Production {
  Server.Port = 443  # Override
}
```

### Step 3: Split Large Files

**V1 - Single file:**
```settex
settings {
  # 500+ lines of configuration...
}
```

**V2 - Modular:**
```settex
# common.settex
let version = "1.0.0"
settings {
  Version = version
}

# logging.settex
settings {
  Logging {
    LogLevel.Default = "Information"
  }
}

# appsettings.settex
include "common.settex"
include "logging.settex"

settings {
  ApplicationName = "MyApp"
}
```

### Step 4: Generate Repeated Structures

**V1:**
```settex
settings {
  Services {
    Service "auth" { Port = 8001 Url = "http://localhost:8001" }
    Service "api" { Port = 8002 Url = "http://localhost:8002" }
    Service "web" { Port = 8003 Url = "http://localhost:8003" }
  }
}
```

**V2:**
```settex
let services = [
  { Name = "auth" Port = 8001 }
  { Name = "api" Port = 8002 }
  { Name = "web" Port = 8003 }
]
let host = "localhost"

settings {
  Services = [
    for s in services {
      item {
        Name = s.Name
        Port = s.Port
        Url = "http://${host}:${s.Port}"
      }
    }
  ]
}
```

### Key Differences

| Feature | V1 | V2 |
|---------|----|----|
| Variables | ❌ Not supported | ✅ `let name = value` |
| Expressions | ❌ Only literals | ✅ Arithmetic, logical, etc. |
| String interpolation | ❌ Not supported | ✅ `"${var}"` |
| Conditional values | ❌ Not supported | ✅ `value if condition` |
| File splitting | ❌ Not supported | ✅ `include "file.settex"` |
| Dynamic arrays | ❌ Manual repetition | ✅ `for x in list { ... }` |
| Set-if-missing | ❌ Not supported | ✅ `Path := value` |

All V1 syntax remains valid in V2 - you can migrate gradually!

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
