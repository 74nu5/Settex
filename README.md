# Settex

**Settex** is a human-friendly configuration language for .NET that compiles to `appsettings.json` files. It provides a clean, concise syntax with support for environment-specific overlays and deep merging.

📖 **Documentation:** [settex.74nu5.dev](https://settex.74nu5.dev)

## ✨ Features

- 🎯 **Simple, intuitive syntax** - No complex markup, just clean configuration
- 🔀 **Environment overlays** - Define base settings once, override per environment
- 🔄 **Deep merging** - Objects merge intelligently, arrays replace completely
- 🧭 **Delta output** - Environment files hold only their overrides, layered by .NET at runtime (opt into full merged files with `--merged`)
- 🚦 **Coverage check** - Warns when a key is set in some environments but forgotten in others
- 🛠️ **MSBuild integration** - Automatic compilation during build
- 🖥️ **CLI tool** - Standalone compiler with beautiful diagnostics
- 📍 **Precise diagnostics** - Line/column error reporting with IDE integration
- ✅ **Conditional writes** - Only updates files when content changes

### V2 Features 🆕

- 📂 **File includes** - Share variables across multiple files
- 🔤 **Variables (`let`)** - Define reusable values with proper scoping
- 🧮 **Expressions** - Arithmetic, logical, comparison, and null coalescing
- 💬 **String interpolation** - Embed variables and expressions in strings: `"${host}:${port}"`
- ❓ **Conditional assignments** - `Value = expr if condition`
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

### Editor Support

Visual Studio and VS Code extensions provide syntax highlighting, snippets, and a language server (hover, go-to-definition, find-references, diagnostics, completion).

> The language server runs on the **.NET 10 runtime** (LTS). In **VS Code you normally install nothing**: the extension depends on Microsoft's [.NET Install Tool](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.vscode-dotnet-runtime), installed automatically alongside Settex, which fetches a private user-local copy on first use — no admin rights, nothing extra in the VSIX. Under **Visual Studio** the runtime usually comes with a .NET workload; otherwise add the *.NET 10.0 Runtime* component. If it can't be found, syntax highlighting and snippets keep working and the extension shows a message with a link to [download .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

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

env "Development" {
  settings {
    Logging.LogLevel.Default = "Debug"
    ConnectionStrings.DefaultConnection = "Server=localhost;Database=MyDb_Dev"
  }
}

env "Production" {
  settings {
    Logging.LogLevel.Default = "Warning"
    ConnectionStrings.DefaultConnection = "Server=prod-server;Database=MyDb"
  }
}
```

> **Syntax note.** An `env` block requires a **quoted** environment name and wraps its overrides in an inner `settings { ... }` block: `env "Development" { settings { ... } }`. This is different from the base `settings` block, which is written at the top level.

Build your project, and Settex will generate:
- `appsettings.json` - Base settings
- `appsettings.Development.json` - Development overrides only (layered on the base by .NET at runtime)
- `appsettings.Production.json` - Production overrides only

> By default each environment file is a **delta** — only what differs from the base — which is exactly how .NET configuration layers `appsettings.{Environment}.json` over `appsettings.json`. Pass `--merged` (CLI) or set `SettexMergeEnvironments=true` (MSBuild) if you'd rather generate full, self-contained environment files.

## 📖 Language Syntax

### Comments

```settex
# Hash-style comment
// Double-slash comment
```

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

Use nested blocks for hierarchical configuration. A named block becomes a JSON key:

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

### Named Child Blocks (maps)

Because a named block becomes a key, you can build a map of named objects by nesting named blocks. The block names must be valid identifiers:

```settex
settings {
  Providers {
    AzureAd {
      ClientId = "abc-123"
      Authority = "https://login.microsoftonline.com"
    }

    Google {
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

### Arrays

Define arrays with comma-separated values. A newline separates items too, so commas may be omitted **across lines** — but two items on the same line still need one:

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

### Object Literals in Arrays

To put objects inside an array, prefix each object with a **tag** — any identifier followed by a block. The tag is a required syntactic label and is **not** emitted in the JSON (it exists so the parser can tell an object element from an expression):

```settex
settings {
  Services = [
    service {
      Name = "auth"
      Port = 5001
    }
    service {
      Name = "data"
      Port = 5002
    }
  ]
}
```

Generates:

```json
{
  "Services": [
    { "Name": "auth", "Port": 5001 },
    { "Name": "data", "Port": 5002 }
  ]
}
```

The same tagged-object form can be used as a plain value: `Database = connection { Host = "localhost" Port = 5432 }` produces `"Database": { "Host": "localhost", "Port": 5432 }` (the `connection` tag is discarded).

> A bare `{ ... }` without a tag is **not** valid — always prefix object literals with an identifier.

### Dot-Path Assignments

Set nested values using dot notation:

```settex
settings {
  Logging.LogLevel.Default = "Information"
  Logging.LogLevel.Microsoft = "Warning"
  Logging.LogLevel.System = "Error"
}
```

### Environment Overlays

Define environment-specific overrides with `env` blocks. Each `env` block takes a **quoted** name and contains an inner `settings` block (and, optionally, environment-scoped `let` variables):

```settex
settings {
  ApiUrl = "https://api.example.com"
  LogLevel = "Information"
}

env "Development" {
  settings {
    ApiUrl = "http://localhost:5000"
    LogLevel = "Debug"
  }
}

env "Staging" {
  settings {
    ApiUrl = "https://staging-api.example.com"
    LogLevel = "Debug"
  }
}

env "Production" {
  settings {
    LogLevel = "Warning"
  }
}
```

Each `env` block generates a separate `appsettings.{Environment}.json` file. By default it contains only that environment's overrides (a delta), which .NET layers over `appsettings.json` at runtime; use `--merged` / `SettexMergeEnvironments` for full merged files. See [Environment output & coverage](#-environment-output--coverage).

### Merging Behavior

- **Objects**: Merged deeply (properties combined)
- **Arrays**: Replaced completely (no merging)
- **Primitives**: Replaced

> **⚠️ Array-layering caveat (a .NET limitation).** The rules above describe how Settex merges values into the *effective* config. At **runtime**, though, .NET loads `appsettings.json` and `appsettings.{Env}.json` as separate layers and merges arrays **by index**, not by replacement. So if the base defines `Hosts = ["a","b","c"]` and an environment overrides it with `Hosts = ["x"]`, the effective runtime value is `["x","b","c"]` — the base's extra elements leak. Settex cannot change how the .NET provider layers arrays, so it **warns at compile time** in the two cases where content leaks:
>
> - the environment's array is **shorter** than the base's, so the base's trailing elements survive;
> - the elements are **objects**, in which case .NET merges the two objects at each shared index *field by field* — so any field the base element defines and the override omits survives, whatever the lengths. `[{ "Name": "a", "Port": 1 }]` overridden by `[{ "Name": "b" }]` yields `Port = 1` at runtime.
>
> To avoid either, keep the array at least as long per environment and repeat every field of an object element, or define the array only per environment (not in the base) so nothing layers under it.
>
> Because .NET configuration keys are **case-insensitive**, all of these checks compare keys that way too: a base `Hosts` and an environment `hosts` are one key at runtime, and are analysed as one.

Example:

```settex
settings {
  Database {
    Host = "localhost"
    Port = 5432
  }
  Tags = ["dev", "test"]
}

env "Production" {
  settings {
    Database.Host = "prod-server"  // Only Host changes
    Tags = ["prod"]                // Array replaced entirely
  }
}
```

Production **effective** configuration (what .NET sees after layering base + overlay):
```json
{
  "Database": {
    "Host": "prod-server",
    "Port": 5432
  },
  "Tags": ["prod"]
}
```

By default the generated `appsettings.Production.json` holds only the delta — `{ "Database": { "Host": "prod-server" }, "Tags": ["prod"] }` — and .NET merges it over the base to produce the effective result above. With `--merged`, the file contains the full effective config directly.

## 🧭 Environment output & coverage

### Delta vs. merged output

Because the whole point of Settex is a single source of truth, the *output* is designed to sit cleanly on top of .NET's native layering:

- **Delta (default):** `appsettings.{Env}.json` contains only the keys that differ from the base. Diffs stay small, base values aren't duplicated into every file, and each key still flows through .NET's per-key layering (environment variables, user-secrets and the command line keep overriding it as usual).
- **Merged (opt-in):** each environment file contains the full, self-contained effective config. Useful when you want to audit or ship a single file per environment.

Switch with `--merged` (CLI) or `<SettexMergeEnvironments>true</SettexMergeEnvironments>` (MSBuild).

### Coverage check

Settex warns when a key is set for **some** environments but missing from the others **and** from the base — the classic "added a key in dev, forgot prod" drift that motivated the project:

```
appsettings.settex: warning: Key 'DevOnly.Flag' is set in 'Development' but missing from 'Production', and is not in the base settings. Add it to the base 'settings' block or to the missing environment(s) to keep configuration consistent.
```

It's **advisory** (a warning, never a build failure) and on by default. Turn it off with `--no-coverage-check` (CLI) or `<SettexCheckCoverage>false</SettexCheckCoverage>` (MSBuild). Under MSBuild it surfaces as a `SETTEX` warning.

> The array-layering check has its **own** switch, `--no-array-layering-check` (`<SettexCheckArrayLayering>false</SettexCheckArrayLayering>` in MSBuild). The two report different hazards, so silencing one no longer silences the other.

**What is *not* flagged, on purpose:** a key set in **every** environment but absent from the base. That configuration is correct as it stands, and the obvious "fix" — inventing a base default — is often worse for values that must be decided per environment (connection strings, endpoints, secret placeholders): a plausible-looking wrong default would apply silently. The deferred risk ("someone adds a new environment and forgets the key") is already covered: the moment that environment exists without the key, the key is in some environments but not all, and the warning above fires.

### Importing an existing configuration

Nobody rewrites a working production configuration by hand. `settex import` takes the
family you already have and produces the `.settex`, **proven equivalent** before it is
written:

```bash
settex import path/to/appsettings.json
#   + environment Development from appsettings.Development.json
#   + environment Production from appsettings.Production.json
# ✓ Imported 2 environment(s); round-trip verified exact.
```

Sibling `appsettings.{Environment}.json` files become `env` blocks automatically.
Before writing anything, the command compiles the generated text through the real
pipeline and compares every flattened key — the way .NET's configuration provider
sees them — against the originals. If a single key differs, nothing is written and
the differences are listed: a migration that is merely *probably* right is worse
than none, because a missed key surfaces at runtime in the environment where it was
missed. Keyword keys (`env`), dotted keys (`"Microsoft.AspNetCore"`) and literal
`${` in values are quoted and escaped as needed.

## 🆕 V2 Features

### File Includes

Split configuration across files with `include`. An included file can contribute `let` variables, `settings` blocks, and `env` blocks — all of them are made available to the including file. This is how you build **modular configuration**.

- **Variables** from an included file are in scope in the including file.
- **`settings` blocks** are deep-merged in document order. Because an include is expanded where it appears, the including file's own `settings` come later and therefore **win** on conflicting keys.
- **`env` blocks** with the same environment name merge the same way, so an included module can add to an environment overlay.

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
```

```settex
# appsettings.settex
include "./common.settex"

settings {
  ApplicationName = "MyApp"
  Server {
    Port = 9090   # overrides the included default
  }
}
```

Generates (base `appsettings.json`):

```json
{
  "Server": { "Host": "localhost", "Port": 9090 },
  "ApplicationName": "MyApp"
}
```

**Rules & features**:
- At least one `settings` block must exist across the file and everything it includes.
- Relative paths are resolved from the file's location.
- Circular includes are detected and reported.
- A `let` variable may only be defined once per scope, so avoid redefining the same global variable in both an included file and the including file.

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

env "Development" {
  let basePort = 5000  # Shadows the global variable within this env
  settings {
    DevUrl = "http://${host}:${basePort}"
  }
}
```

**Scoping rules**:
- **Global scope**: variables defined at file level
- **Environment scope**: `let` inside an `env` block shadows global variables for that environment
- **For-loop scope**: iterator variables only exist within the loop body

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
  IsLargeTimeout = timeout > 60

  # Logical
  ShouldCache = enabled and timeout > 10
  DebugMode = not enabled

  # Null coalescing
  LogLevel = null ?? "Information"
  Host = null ?? "localhost"
}
```

**Supported operators**:
- Arithmetic: `+`, `-`, `*`, `/`
- Comparison: `==`, `!=`, `<`, `<=`, `>`, `>=` (numeric operands for the ordering operators)
- Logical: `and`, `or`, `not`
- Null coalescing: `??`

**Grouping with parentheses** overrides the default precedence:

```settex
settings {
  A = (10 + 5) * 2      # 30, not 20
  B = (true or false) and not false
}
```

**`+` concatenates when either operand is a string**, coercing numbers and booleans to their string form (as interpolation does); with two numbers it adds:

```settex
let version = "2"
let port = 8080

settings {
  Label = "v" + version + ".0"   # "v2.0"
  Url = "http://host:" + port     # "http://host:8080"
  Sum = 8000 + 80                 # 8080 (numeric)
}
```

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

### Keys containing a dot

The dot separates path segments, so `Logging.LogLevel.Microsoft.AspNetCore` means four
nested keys. .NET flattens configuration with a **colon** and treats a dot as an
ordinary character, so `Logging:LogLevel:Microsoft.AspNetCore` — the log-level filter
every ASP.NET Core app sets — needs a literal dot inside one key.

**Quote the segment** to say that:

```settex
settings {
  Logging {
    LogLevel {
      Default = "Information"
      "Microsoft.AspNetCore" = "Warning"   # one key, with a dot in it
    }
  }
  "Content-Type" = "application/json"     # also works for any other character
}
```

A quoted segment is accepted anywhere an identifier is — on its own, in the middle of a
dotted path (`Nested."A.B".C`), and as a nested block name. It may not contain an
interpolation: a key has to be known before anything is evaluated, so `"${x}"` is
refused rather than silently taken literally.

**Escaping** — write `$${` for a literal `${`, so a string can legitimately contain `${...}` without Settex evaluating it:

```settex
settings {
  HomePath = "$${HOME}/bin"                    # "${HOME}/bin" — left alone
  Mixed    = "$${NOT_INTERP} and ${realVar}"   # escape and interpolation in one string
}
```

> **A literal `$` directly before an interpolation.** `$$` is only special when a `{`
> follows it, so `"$${price}"` is read as the escape and produces the literal
> `${price}` — not `$` followed by the value of `price`. There is no escape for that
> combination; concatenate instead: `"$" + "${price}"`. The case is narrow (a currency
> symbol immediately before a placeholder) but silent, so it is worth knowing.

**Note**: Interpolating `null` throws an error. Use null coalescing: `"${value ?? "default"}"`.

> `${...}` is resolved **at compile time** from Settex variables and expressions — it is not a placeholder for runtime environment variables. A name like `${DB_CONNECTION_STRING}` is looked up as a Settex variable and fails if undefined. Inject secrets at deployment time through .NET's environment-variable or user-secrets configuration providers rather than through `.settex`.

### Conditional Assignments

Assign a value only when a boolean condition holds:

```settex
env "Development" {
  let debug = true
  settings {
    LogLevel = "Debug" if debug
    MaxConnections = 100 if debug
  }
}
```

**Rules**:
- The condition must evaluate to `bool`.
- Statements run top to bottom. A conditional whose condition is `false` leaves any value assigned earlier in place, so write the unconditional fallback **first** and the conditional override **after**:
  ```settex
  let verbose = false

  settings {
    LogLevel = "Warning"          # fallback, always assigned
    LogLevel = "Debug" if verbose # overrides only when `verbose` is true
  }
  ```
- Conditions are evaluated in the **current scope**. The base `settings` block only sees global `let` values, so for per-environment behavior put the conditional inside the relevant `env` block's `settings` (or simply assign the value there — that is what overlays are for).

### Set-If-Missing Operator (`:=`)

Set a value only if the path is not already defined:

```settex
settings {
  Server {
    Port := 8080          # Sets a default
    MaxConnections := 100 # Sets a default
  }
}

env "Development" {
  settings {
    Server {
      Port = 5000            # Overrides with =
      MaxConnections := 50   # Won't set (already defined in base)
    }
  }
}

env "Production" {
  settings {
    Server {
      Port := 443            # Won't set (already defined in base)
      MaxConnections = 1000  # Overrides with =
    }
  }
}
```

**Rules**:
- `:=` only sets a value if the path does not exist yet.
- In environment overlays, it checks the base settings first.
- Useful for providing defaults that environments can override.

### For Loops

Generate array elements dynamically. Each iteration must produce exactly one tagged object (the tag — `item` below — is a discarded label):

```settex
let ports = [8001, 8002, 8003]
let host = "localhost"

settings {
  # Iterate over a list of values
  ServiceUrls = [
    for port in ports {
      item { Url = "http://${host}:${port}" }
    }
  ]

  # Iterate over an inline list
  Numbered = [
    for i in [1, 2, 3] {
      item { Index = i }
    }
  ]
}
```

Iterating over tagged objects gives access to their fields with member access:

```settex
let services = [
  svc { Name = "auth" Port = 8001 }
  svc { Name = "api"  Port = 8002 }
]
let host = "localhost"

settings {
  Endpoints = [
    for s in services {
      item {
        Name = s.Name
        Url = "http://${host}:${s.Port}"
      }
    }
  ]
}
```

**Features**:
- The iterator variable is scoped to the loop body and can read outer-scope variables.
- The loop body must contain exactly one tagged block.
- For loops nest.

### Complete V2 Example

A real-world example using the V2 features:

```settex
# common.settex
let baseHost = "localhost"
let basePort = 8000
let services = [
  svc { Name = "auth" Port = 8001 }
  svc { Name = "api"  Port = 8002 }
]
```

```settex
# appsettings.settex
include "./common.settex"

settings {
  ApplicationName = "MyApp"
  Version = "2.0.0"

  Server {
    Host := baseHost
    Port := basePort
    MaxConnections := 100
  }

  BaseUrl = "http://${baseHost}:${basePort}"

  Endpoints = [
    for s in services {
      item {
        Name = s.Name
        Url = "http://${baseHost}:${s.Port}"
      }
    }
  ]

  Logging {
    LogLevel {
      Default = "Information"
    }
  }
}

env "Development" {
  let basePort = 5000
  settings {
    Server.Port = basePort
    BaseUrl = "http://dev.${baseHost}:${basePort}"
    Logging.LogLevel.Default = "Debug"
  }
}

env "Production" {
  let prodHost = "api.example.com"
  settings {
    Server.Port = 443
    Server.MaxConnections = 1000
    BaseUrl = "https://${prodHost}"
    Logging.LogLevel.Default = "Warning"
  }
}
```

For more examples, see the `samples/` directory.

## 🔄 Migration Guide: V1 → V2

V2 is backward compatible with V1 configuration constructs (settings, nested objects, arrays, dot-paths, and environment overlays). You can adopt the new features incrementally.

### Step 1: Extract Common Values

**V1:**
```settex
settings {
  Server.Host = "localhost"
  Server.Port = 8080
  Database.Host = "localhost"
  Database.Port = 5432
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
```

### Step 2: Use Set-If-Missing for Defaults

**V1:**
```settex
settings {
  Server.Port = 8080
}

env "Development" {
  settings {
    Server.Port = 8080  # Repeated
  }
}

env "Production" {
  settings {
    Server.Port = 443
  }
}
```

**V2:**
```settex
settings {
  Server.Port := 8080  # Default
}

env "Development" {
  settings {
    # Inherits default 8080
  }
}

env "Production" {
  settings {
    Server.Port = 443  # Override
  }
}
```

### Step 3: Split Shared Variables Into Includes

```settex
# common.settex
let version = "1.0.0"
let defaultLogLevel = "Information"
```

```settex
# appsettings.settex
include "./common.settex"

settings {
  ApplicationName = "MyApp"
  Version = version
  Logging.LogLevel.Default = defaultLogLevel
}
```

### Step 4: Generate Repeated Structures

**V1:**
```settex
settings {
  Services {
    Auth { Port = 8001 Url = "http://localhost:8001" }
    Api  { Port = 8002 Url = "http://localhost:8002" }
    Web  { Port = 8003 Url = "http://localhost:8003" }
  }
}
```

**V2:**
```settex
let services = [
  svc { Name = "auth" Port = 8001 }
  svc { Name = "api"  Port = 8002 }
  svc { Name = "web"  Port = 8003 }
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
| Shared variables | ❌ Not supported | ✅ `include "file.settex"` |
| Dynamic arrays | ❌ Manual repetition | ✅ `for x in list { item { ... } }` |
| Set-if-missing | ❌ Not supported | ✅ `Path := value` |

## 🔧 MSBuild Configuration

The Settex.Build package automatically discovers `*.settex` files in your project. You can customize the behavior:

```xml
<PropertyGroup>
  <!-- Change output directory (default: project root) -->
  <SettexOutputDirectory>$(MSBuildProjectDirectory)\config</SettexOutputDirectory>

  <!-- Full merged env files instead of overrides-only (default: false) -->
  <SettexMergeEnvironments>false</SettexMergeEnvironments>

  <!-- Warn about keys set in some environments but not others (default: true) -->
  <SettexCheckCoverage>true</SettexCheckCoverage>

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

# Write full merged config in each env file (default: overrides only)
settex build appsettings.settex --merged

# Skip the cross-environment coverage check
settex build appsettings.settex --no-coverage-check

# Show help
settex --help
settex build --help
```

The CLI provides:
- ✨ Beautiful formatted diagnostics with Spectre.Console
- 🎨 Color-coded error messages
- 📍 Precise error locations

## 📝 Value Types

Settex supports these value types:

- **Strings**: `"Hello World"` (double quotes only)
- **Numbers**: `42`, `3.14`, `-10`
- **Booleans**: `true`, `false`
- **Null**: `null`
- **Arrays**: `[1, 2, 3]` or multiline
- **Objects**: Nested blocks, or tagged object literals (`tag { ... }`)

## ⚠️ Error Handling

Settex provides precise error diagnostics with file, line, and column:

```
appsettings.settex(12,5): error: Expected '=' after path
```

Errors include:
- File name and location (line, column)
- A clear error message
- IDE integration (clickable in Visual Studio, VS Code, Rider)

Compilation stops at the first error in each phase (lexing, parsing, include resolution, evaluation, writing).

## 🏗️ Project Structure

```
MyProject/
├── appsettings.settex           # Your configuration source
├── appsettings.json             # Generated (base)
├── appsettings.Development.json # Generated (dev overrides only)
├── appsettings.Production.json  # Generated (prod overrides only)
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
      "Microsoft.AspNetCore" = "Warning"
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

env "Development" {
  settings {
    Logging.LogLevel.Default = "Debug"
    ConnectionStrings.DefaultConnection = "Server=localhost;Database=MyApi_Dev"
    JwtSettings.ExpirationMinutes = 1440
  }
}

env "Production" {
  settings {
    Logging.LogLevel.Default = "Warning"
    Logging.LogLevel."Microsoft.AspNetCore" = "Error"
    # Keep real secrets out of source: use a non-secret placeholder here and
    # override at runtime via the environment / user-secrets providers.
    ConnectionStrings.DefaultConnection = "Server=prod-db;Database=MyApi"
    JwtSettings.SecretKey = "set-via-environment"
  }
}
```

### Multi-Service Configuration

```settex
settings {
  Services {
    EmailService {
      Enabled = true
      Provider = "SendGrid"
      ApiKey = "dev-key"
    }

    StorageService {
      Enabled = true
      Provider = "LocalDisk"
      RootPath = "./storage"
    }

    CacheService {
      Enabled = false
      Provider = "Memory"
    }
  }
}

env "Production" {
  settings {
    Services {
      # Real credentials should be injected at runtime, not stored here.
      EmailService {
        ApiKey = "set-via-environment"
      }

      StorageService {
        Provider = "AzureBlobStorage"
        ConnectionString = "set-via-environment"
      }

      CacheService {
        Enabled = true
        Provider = "Redis"
        ConnectionString = "set-via-environment"
      }
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

- [Documentation](https://settex.74nu5.dev)
- [GitHub Repository](https://github.com/74nu5/Settex)
- [NuGet Package (Build)](https://nuget.org/packages/Settex.Build)
- [NuGet Package (CLI)](https://nuget.org/packages/Settex.Cli)
