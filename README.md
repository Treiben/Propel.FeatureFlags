# Propel.FeatureFlags

[![Build](https://github.com/tasriyan/Propel.FeatureFlags/actions/workflows/build.yml/badge.svg)](https://github.com/tasriyan/Propel.FeatureFlags/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Propel.FeatureFlags.svg)](https://www.nuget.org/packages/Propel.FeatureFlags/)
[![Framework](https://img.shields.io/badge/.NET-Standard%202.0%20%7C%20.NET%209.0-blue)](https://dotnet.microsoft.com/)
[![Language](https://img.shields.io/badge/language-C%23-239120.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](LICENSE)

A type-safe feature flag library for .NET with support for targeting, scheduling, and rollout strategies.

## Features

- **Type Safety**: Compile-time flag validation prevents runtime errors
- **Evaluation Modes**: Toggle, scheduled, time windows, user/tenant targeting, percentage rollouts, custom rules
- **Auto-Creation**: Application flags created automatically on deployment
- **Storage Options**: PostgreSQL, SQL Server support via separate packages
- **Caching**: Redis and in-memory caching
- **ASP.NET Core Integration**: Middleware and HTTP context extensions
- **Attribute Support**: Optional AOP-style flag evaluation

## Demo Project

See the complete working example in `/usage-demo/WebClientDemo` - a client API application demonstrating minimal API endpoints with feature flag integration.

## Basic Flag Setup and Usage

### 1. Install Core Packages
```bash
dotnet add package Propel.FeatureFlags
dotnet add package Propel.FeatureFlags.AspNetCore
```

### 2. Register Services
```csharp
var builder = WebApplication.CreateBuilder(args);

// Core feature flag services
builder.Services.AddPropelServices();
builder.Services.AddPropelFeatureFlags();

var app = builder.Build();
app.UseFeatureFlags();
```

### 3. Define Type-Safe Flag
```csharp
public class NewProductApiFeatureFlag : FeatureFlagBase
{
    public NewProductApiFeatureFlag() 
        : base(key: "new-product-api", 
            name: "New Product API", 
            description: "Controls whether to use the new enhanced product API implementation",
            onOfMode: EvaluationMode.Off)
    {
    }
}
```

### 4. Use in Endpoints
```csharp
// With HttpContext
app.MapGet("/products", async (HttpContext context) =>
{
    if (await context.IsFeatureFlagEnabledAsync(new NewProductApiFeatureFlag()))
    {
        return Results.Ok("New API implementation");
    }
    return Results.Ok("Legacy API implementation");
});

// With Factory Pattern
app.MapGet("/products", async (HttpContext context, IFeatureFlagFactory flags) =>
{
    var flag = flags.GetFlagByType<NewProductApiFeatureFlag>();
    if (await context.IsFeatureFlagEnabledAsync(flag))
    {
        return Results.Ok("New API implementation");
    }
    return Results.Ok("Legacy API implementation");
});
```

## Attribute-Based Usage

Aspect-Oriented Programming (AOP) style flag evaluation allows you to decorate methods with feature flags. When the flag is disabled, it automatically calls a fallback method.

### 1. Install Attributes Package
```bash
dotnet add package Propel.FeatureFlags.Attributes
```

### 2. Register Attribute Interceptors
```csharp
// For HttpContext-based evaluation (user/tenant targeting)
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpAttributeInterceptors();

// For simple evaluation without HttpContext
builder.Services.AddAttributeInterceptors();

// Register service with interceptor support
builder.Services.RegisterServiceWithAttributes<INotificationService, NotificationService>();
```

### 3. Use FeatureFlagged Attribute
```csharp
public interface INotificationService
{
    Task<string> SendEmailAsync(string userId, string subject, string body);
    Task<string> SendEmailLegacyAsync(string userId, string subject, string body);
}

public class NotificationService : INotificationService
{
    [FeatureFlagged(type: typeof(NewEmailServiceFeatureFlag), fallbackMethod: nameof(SendEmailLegacyAsync))]
    public virtual async Task<string> SendEmailAsync(string userId, string subject, string body)
    {
        // New implementation - called when flag is enabled
        return "Email sent using new service.";
    }

    public virtual async Task<string> SendEmailLegacyAsync(string userId, string subject, string body)
    {
        // Fallback implementation - called when flag is disabled
        return "Email sent using legacy service.";
    }
}
```

## Database Configuration

### 1. Install Database Package
```bash
# PostgreSQL
dotnet add package Propel.FeatureFlags.Infrastructure.PostgresSql

# SQL Server
dotnet add package Propel.FeatureFlags.Infrastructure.SqlServer
```

### 2. Configure Connection
```csharp
var pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("PostgreSQL connection string is required");

builder.Services.AddPropelPersistence(pgConnectionString);
```

### 3. Database Initialization
```csharp
var app = builder.Build();

// Automatic database setup and flag registration
await app.EnsureDatabase();

// With optional SQL seeding for development
await app.EnsureDatabase(sqlScriptFile: "seed-db.sql");
```

**Database initialization includes:**
- Creates database and tables if they don't exist
- Automatically registers all defined flags from code
- Optional SQL script seeding for development/testing
- Fails fast in production, continues in development on errors

## Cache Configuration

### Redis Distributed Cache
```bash
dotnet add package Propel.FeatureFlags.Infrastructure.Redis
```

```csharp
builder.Services.AddPropelDistributedCache("localhost:6379");
```

### In-Memory Cache
```csharp
builder.Services.AddPropelInMemoryCache();
```

### Cache Options
- **Distributed Cache**: Shared across multiple application instances, requires Redis
- **In-Memory Cache**: Per-instance caching, faster but not shared
- **No Cache**: Direct database queries, not recommended for production

## Flag Configuration in JSON

### Application Settings
```json
{
  "PropelFeatureFlags": {
    "database": {
      "DefaultConnection": "Host=localhost;Port=5432;Database=propel;Username=user;Password=password"
    },
    "cache": {
      "EnableDistributedCache": true,
      "EnableInMemoryCache": false,
      "CacheDurationInMinutes": 30,
      "SlidingDurationInMinutes": 15,
      "Connection": "localhost:6379"
    },
    "DefaultTimeZone": "UTC"
  }
}
```

### Loading Configuration
```csharp
var propelOptions = builder.Configuration.GetSection("PropelFeatureFlags").Get<PropelOptions>() ?? new();
builder.Services.AddPropelServices(propelOptions);
```

## Middleware Configuration

The library supports different middleware scenarios for various use cases:

### Basic Configuration
```csharp
app.UseFeatureFlags(); // Default configuration
```

### Maintenance Mode
```csharp
app.UseFeatureFlags(options =>
{
    options.EnableMaintenanceMode = true;
    options.MaintenanceFlagKey = "api-maintenance";
    options.MaintenanceResponse = new
    {
        message = "API is temporarily down for maintenance",
        estimatedDuration = "30 minutes",
        contact = "support@company.com"
    };
});
```

### SaaS with Custom Attributes
```csharp
app.UseFeatureFlags(options =>
{
    // Custom user ID extraction
    options.UserIdExtractor = context =>
    {
        var jwtUserId = context.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(jwtUserId)) return jwtUserId;
        
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey)) return $"api:{apiKey}";
        
        return context.Request.Headers["X-Session-ID"].FirstOrDefault();
    };

    // Extract tenant and user attributes for targeting
    options.AttributeExtractors.Add(context =>
    {
        var attributes = new Dictionary<string, object>();
        
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId))
            attributes["tenantId"] = tenantId.ToString();
            
        if (context.Request.Headers.TryGetValue("Role", out var role))
            attributes["role"] = role.ToString();
            
        var userTier = context.User.FindFirst("tier")?.Value;
        if (!string.IsNullOrEmpty(userTier))
            attributes["userTier"] = userTier;
            
        return attributes;
    });
});
```

### Advanced Configuration
```csharp
app.UseFeatureFlags(options =>
{
    // Combine maintenance mode with custom attribute extraction
    options.EnableMaintenanceMode = true;
    options.MaintenanceFlagKey = "api-maintenance";
    
    // Extract headers for targeting rules
    options.AttributeExtractors.Add(context =>
    {
        var attributes = new Dictionary<string, object>();
        
        if (context.Request.Headers.TryGetValue("Department", out var department))
            attributes["department"] = department.ToString();
            
        if (context.Request.Headers.TryGetValue("Country", out var country))
            attributes["country"] = country.ToString();
            
        if (context.Request.Headers.TryGetValue("User-Type", out var userType))
            attributes["userType"] = userType.ToString();
            
        return attributes;
    });
});
```

## Factory Pattern

For organized flag management in larger codebases:

### Interface Definition
```csharp
public interface IFeatureFlagFactory
{
    IFeatureFlag? GetFlagByKey(string key);
    IFeatureFlag? GetFlagByType<T>() where T : IFeatureFlag;
    IEnumerable<IFeatureFlag> GetAllFlags();
}
```

### Implementation
```csharp
public class DemoFeatureFlagFactory : IFeatureFlagFactory
{
    private readonly HashSet<IFeatureFlag> _allFlags;

    public DemoFeatureFlagFactory(IEnumerable<IFeatureFlag> allFlags)
    {
        _allFlags = allFlags.ToHashSet();
    }

    public IFeatureFlag? GetFlagByKey(string key) => 
        _allFlags.FirstOrDefault(f => f.Key == key);

    public IFeatureFlag? GetFlagByType<T>() where T : IFeatureFlag => 
        _allFlags.OfType<T>().FirstOrDefault();

    public IEnumerable<IFeatureFlag> GetAllFlags() => [.. _allFlags];
}
```

### Registration
```csharp
builder.Services.AddSingleton<IFeatureFlagFactory, DemoFeatureFlagFactory>();
```

## Evaluation Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| `Off`| Flag is disabled | Kill switches, disable features |
| `On` | Flag is enabled | Enable features, simple toggles |
| `Scheduled` | Time-based activation | Coordinated releases, campaigns |
| `TimeWindow` | Daily/weekly time ranges | Business hours, maintenance windows |
| `UserTargeted` | Specific user allowlists/blocklists | Beta users, specific targeting |
| `UserRolloutPercentage` | Percentage-based user rollout | Gradual rollouts, A/B testing |
| `TenantRolloutPercentage` | Percentage-based tenant rollout | Multi-tenant gradual rollouts |
| `TenantTargeted` | Specific tenant allowlists/blocklists | Tenant-specific features |
| `TargetingRules` | Custom attribute-based rules | Complex targeting scenarios |

## Available Packages

| Package | Purpose | Target Framework |
|---------|---------|------------------|
| `Propel.FeatureFlags` | Core library | .NET Standard 2.0 |
| `Propel.FeatureFlags.AspNetCore` | ASP.NET Core integration | .NET 9.0 |
| `Propel.FeatureFlags.Infrastructure.PostgresSql` | PostgreSQL storage | .NET 9.0 |
| `Propel.FeatureFlags.Infrastructure.Redis` | Redis caching | .NET Standard 2.0 |
| `Propel.FeatureFlags.Attributes` | AOP-style evaluation | .NET 9.0 |

## Best Practices

See [best-practices-readme.md](best-practices-readme.md) for implementation guidance and recommended patterns.

## License

Apache-2.0 License - see [LICENSE](LICENSE) file for details.

## Contributing

Contributions welcome. Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.