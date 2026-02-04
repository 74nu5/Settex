# Settex V2 - Complete Feature Showcase

This sample demonstrates all implemented V2 features.

## Files

- **common.settex**: Shared configuration imported by main file
- **complete-v2-features.settex**: Comprehensive example using all V2 features

## Features Demonstrated

### 1. Include System ✅
```settex
include "./common.settex"
```
- Import shared configuration
- Variables from included files are available
- Cycle detection prevents infinite loops

### 2. Variables (let) ✅
```settex
let appName = "Settex Demo"
let basePort = 8000
let apiPort = basePort + 100
```
- Global variables
- Environment-scoped variables (override global)
- For-loop iterator variables (local scope)
- Expression evaluation in variable declarations

### 3. Expressions ✅

#### Arithmetic
```settex
let result = (10 + 5) * 2 - 3
Server.Port = basePort + 100
```

#### Logical
```settex
let isDev = environment == "Development"
let canAccess = isAdmin and hasPermission
```

#### Comparison
```settex
Database.Enabled = priority < 10
Features.Advanced = version >= 2
```

#### Null Coalescing
```settex
DefaultValue = maybeNull ?? "fallback"
```

### 4. String Interpolation ✅
```settex
ApplicationUrl = "https://${host}:${port}"
ConnectionString = "Server=${dbHost};Port=${dbPort}"
```
- Embed any expression in strings
- Null values cause errors (safe)

### 5. Conditional Assignments (if inline) ✅
```settex
LogLevel = "Debug" if environment == "Development"
LogLevel = "Warning" if environment == "Production"
```
- Assignment only applied if condition is true
- Conditions must evaluate to boolean
- Implicit `env` variable available

### 6. Set-If-Missing Operator (:=) ✅
```settex
Server.Port := 8080
Server.Timeout := 30
```
- Sets value only if key doesn't exist
- Checked in both current overlay and base settings
- Perfect for default values that can be overridden

### 7. For Loops in Arrays ✅
```settex
Services = [
    for s in serviceList {
        item {
            Name = s.Name
            Url = "http://${host}:${s.Port}"
        }
    }
]
```
- Iterate over array variables
- Generate multiple items from templates
- Iterator variable is local to loop body
- Access outer scope variables

## Running the Sample

To compile this sample (requires Settex V2 compiler):

```bash
# Using CLI
settex compile complete-v2-features.settex

# Output files:
# - appsettings.json (base)
# - appsettings.Development.json
# - appsettings.Staging.json
# - appsettings.Production.json
```

## Sample Output Structure

The sample generates complete appsettings for a modern web API with:
- **Server configuration** (host, port, timeout)
- **Database connections** (with priorities and failover)
- **Microservices** (dynamically generated from list)
- **Logging** (environment-specific levels)
- **Features** (conditional feature flags)
- **Security** (HTTPS, CORS, rate limiting)

## Key Patterns

### Pattern 1: Shared Constants
Use `common.settex` for values shared across environments:
- Default ports
- Service names
- Feature flags
- Retry policies

### Pattern 2: Environment Variables
Override global variables in env blocks:
```settex
# Global
let host = "localhost"

env "Production" {
    let host = "api.example.com"  # Overrides global
}
```

### Pattern 3: Default Values with :=
Set sensible defaults that environments can override:
```settex
settings {
    Server.Port := 8080  # Default
}

env "Production" {
    settings {
        Server.Port = 443  # Override
    }
}
```

### Pattern 4: Dynamic Arrays with For Loops
Generate repetitive configuration from data:
```settex
let services = [
    service { Name = "auth" Port = 5001 }
    service { Name = "api" Port = 5002 }
]

settings {
    Services = [
        for s in services {
            item {
                Name = s.Name
                Url = "http://localhost:${s.Port}"
            }
        }
    ]
}
```

## Notes

- All features work together seamlessly
- Variables are lexically scoped
- Expressions are type-safe (runtime errors for invalid operations)
- String interpolation fails on null (explicit handling required)
- For loops create child scopes for iterator variables
