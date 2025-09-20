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

## Quick Start

### 1. Install Packages
```bash
dotnet add package Propel.FeatureFlags
dotnet add package Propel.FeatureFlags.AspNetCore
dotnet add package Propel.FeatureFlags.Infrastructure.PostgresSql
```

### 2. Configure Services
```csharp
var builder = WebApplication.CreateBuilder(args);

// Core feature flags
builder.Services.AddPropelServices();
builder.Services.AddPropelFeatureFlags();

// Storage
builder.Services.AddPropelPersistence(connectionString);

// Caching (optional)
builder.Services.AddPropelDistributedCache(redisConnectionString);

var app = builder.Build();
app.UseFeatureFlags();
```

### 3. Define Type-Safe Flags
```csharp
public class NewCheckoutFeatureFlag : TypeSafeFeatureFlag
{
    public NewCheckoutFeatureFlag() : base(
        key: "new-checkout-flow",
        name: "New Checkout Flow",
        description: "Enhanced checkout with one-click purchasing",
        defaultMode: EvaluationMode.Disabled)
    {
    }
}
```

### 4. Evaluate Flags
```csharp
// In controllers/endpoints
app.MapGet("/checkout", async (HttpContext context) =>
{
    if (await context.IsFeatureFlagEnabledAsync(new NewCheckoutFeatureFlag()))
    {
        return Results.Ok("New checkout experience");
    }
    return Results.Ok("Standard checkout");
});

// With variations
var algorithm = await context.GetFeatureFlagVariationAsync(
    new RecommendationAlgorithmFlag(), 
    "collaborative-filtering");
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

## Architecture

### Application vs Global Flags

**Application Flags**: Auto-created when first evaluated, managed by development teams
```csharp
await context.IsFeatureFlagEnabledAsync(new MyAppFeatureFlag());
```

**Global Flags**: Explicitly created, managed via configuration
```csharp
app.UseFeatureFlags(options => {
    options.GlobalFlags.Add(new GlobalFlag {
        Key = "maintenance-mode",
        StatusCode = 503,
        Response = new { message = "Service temporarily unavailable" }
    });
});
```

## Advanced Configuration

### Middleware Options
```csharp
app.UseFeatureFlags(options =>
{
    // Maintenance mode
    options.EnableMaintenanceMode = true;
    options.MaintenanceFlagKey = "api-maintenance";
    
    // Custom user ID extraction
    options.UserIdExtractor = context => 
        context.User.FindFirst("sub")?.Value;
        
    // Custom attributes for targeting
    options.AttributeExtractors.Add(context => new Dictionary<string, object>
    {
        ["userTier"] = context.User.FindFirst("tier")?.Value ?? "free",
        ["country"] = context.Request.Headers["Country"].FirstOrDefault() ?? "US"
    });
});
```

### Attribute-Based Evaluation
```csharp
// Install: Propel.FeatureFlags.Attributes
builder.Services.AddHttpAttributeInterceptors();
builder.Services.RegisterServiceWithAttributes<IEmailService, EmailService>();

public class EmailService : IEmailService
{
    [FeatureFlagged(typeof(NewEmailProviderFlag), fallbackMethod: nameof(SendLegacyEmail))]
    public virtual async Task<string> SendEmailAsync(string to, string subject, string body)
    {
        // New implementation
        return await newProvider.SendAsync(to, subject, body);
    }
    
    public virtual async Task<string> SendLegacyEmail(string to, string subject, string body)
    {
        // Fallback implementation
        return await legacyProvider.SendAsync(to, subject, body);
    }
}
```

## Available Packages

| Package | Purpose | Target Framework |
|---------|---------|------------------|
| `Propel.FeatureFlags` | Core library | .NET Standard 2.0 |
| `Propel.FeatureFlags.AspNetCore` | ASP.NET Core integration | .NET 9.0 |
| `Propel.FeatureFlags.Infrastructure.PostgresSql` | PostgreSQL storage | .NET 9.0 |
| `Propel.FeatureFlags.Infrastructure.Redis` | Redis caching | .NET Standard 2.0 |
| `Propel.FeatureFlags.Attributes` | AOP-style evaluation | .NET 9.0 |

## Configuration

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

## Database Deployment

### Connection Configuration

```csharp
// 1. Standard ConnectionStrings section (preferred)
var pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    // 2. Fallback to PropelFeatureFlags configuration section  
    ?? propelOptions.Database.DefaultConnection
    ?? throw new InvalidOperationException("PostgreSQL connection string is required");

builder.Services.AddPropelPersistence(pgConnectionString);
```
No separate database deployments or migrations needed for the flags database.

```csharp
var app = builder.Build();

// Automatic database initialization
await app.EnsureDatabase();
```

### Zero-Config Flag Deployment

After you defined your type-safe flags in code, you can automatically register them from your application startup.

```csharp
// Developer defines this in code
public class NewCheckoutFeatureFlag : TypeSafeFeatureFlag { ... }

// App automatically registers it in database on startup - no manual step needed
await app.DeployFlagsAsync(); // Called automatically by EnsureDatabase()
```

**Implementation approaches:**

Factory-based registration (recommended for organized codebases)
```csharp
var factory = serviceProvider.GetRequiredService<IFeatureFlagFactory>();
var allFlags = factory.GetAllFlags();
foreach (var flag in allFlags)
{
    await flag.RegisterPropelFlagsAsync(repository);
}
```

Assembly scanning (alternative)
```csharp
// Automatically finds all TypeSafeFeatureFlag implementations
var currentAssembly = Assembly.GetExecutingAssembly();
var allFlags = currentAssembly
	.GetTypes()
	.Where(t => typeof(IRegisteredFeatureFlag).IsAssignableFrom(t)
			&& !t.IsInterface
			&& !t.IsAbstract);
foreach (var flag in allFlags)
{
	var instance = (IRegisteredFeatureFlag)Activator.CreateInstance(flag)!;
	await instance.RegisterPropelFlagsAsync(repository);
}
```

**Development vs Production:**

- **Production**: Only automatic flag registration from code
- **Development**: Optional SQL script seeding for test data (NOT recommended for production)

## Best Practices

See [best-practices-readme.md](best-practices-readme.md) for implementation guidance and recommended patterns.

## License

Apache-2.0 License - see [LICENSE](LICENSE) file for details.

## Contributing

Contributions welcome. Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.