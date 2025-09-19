# Propel.FeatureFlags

A type-safe, extensible feature flag library for .NET with support for advanced targeting, scheduling, and rollout strategies.

## Features

- **Type Safety**: Compile-time flag validation prevents typos and runtime errors
- **Multiple Evaluation Modes**: Toggle, scheduled, time windows, user/tenant targeting, percentage rollouts, A/B testing
- **Auto-Creation**: Application flags created automatically on deployment for zero-config scenarios
- **Extensible Storage**: PostgreSQL, SQL Server, MongoDB support via separate packages
- **Caching**: Redis and in-memory caching options
- **ASP.NET Core Integration**: Middleware for global flags, maintenance mode, and context extraction
- **AOP Support**: Optional attribute-based flag evaluation via interceptors

## Quick Start

### 1. Install Core Package
```bash
dotnet add package Propel.FeatureFlags
dotnet add package Propel.FeatureFlags.AspNetCore
dotnet add package Propel.FeatureFlags.Infrastructure.PostgresSql
```

### 2. Configure Services
```csharp
var builder = WebApplication.CreateBuilder(args);

// Core feature flags
builder.Services.AddFeatureFlags();

// Storage (choose one)
builder.Services.AddPostgresSqlFeatureFlags(connectionString);

// Caching (optional)
builder.Services.AddRedisCache(redisConnectionString);

// Middleware
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

## Flag Evaluation Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| `Disabled/Enabled` | Simple on/off toggle | Kill switches, feature toggles |
| `Scheduled` | Time-based activation | Coordinated releases, campaigns |
| `TimeWindow` | Daily/weekly time ranges | Business hours, maintenance windows |
| `UserTargeted` | Specific user lists + rollout % | Beta testing, gradual rollouts |
| `TenantTargeted` | Tenant-specific rules | Multi-tenant SaaS features |
| `TargetingRules` | Custom attribute-based rules | Complex targeting scenarios |

## Architecture

### Application vs Global Flags

**Application Flags**: Auto-created, managed by development teams
```csharp
// Auto-created on first evaluation if missing
await context.IsFeatureFlagEnabledAsync(new MyAppFeatureFlag());
```

**Global Flags**: Explicitly created, managed via admin tools
```csharp
// Must exist in system, throws if missing
app.UseFeatureFlags(options => {
    options.GlobalFlags.Add(new GlobalFlag {
        Key = "maintenance-mode",
        StatusCode = 503,
        Response = new { message = "Service temporarily unavailable" }
    });
});
```

## Advanced Usage

### Middleware Configuration
```csharp
app.UseFeatureFlags(options =>
{
    // Maintenance mode
    options.EnableMaintenanceMode = true;
    options.MaintenanceFlagKey = "api-maintenance";
    
    // Custom context extraction
    options.UserIdExtractor = context => 
        context.User.FindFirst("sub")?.Value;
        
    options.AttributeExtractors.Add(context => new Dictionary<string, object>
    {
        ["userTier"] = context.User.FindFirst("tier")?.Value ?? "free",
        ["country"] = context.Request.Headers["Country"].FirstOrDefault() ?? "US"
    });
});
```

### Attribute-Based Evaluation (Optional)
```csharp
// Install: Propel.FeatureFlags.Attributes
builder.Services.AddHttpFeatureFlagsAttributes();
builder.Services.AddScopedWithFeatureFlags<IEmailService, EmailService>();

public class EmailService : IEmailService
{
    [FeatureFlagged(typeof(NewEmailProviderFlag), fallbackMethod: nameof(SendLegacyEmail))]
    public virtual async Task<string> SendEmailAsync(string to, string subject, string body)
    {
        // New email provider implementation
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

| Package | Purpose | Required |
|---------|---------|----------|
| `Propel.FeatureFlags` | Core library with domain logic | ✅ |
| `Propel.FeatureFlags.AspNetCore` | ASP.NET Core middleware and extensions | ✅ |
| `Propel.FeatureFlags.Infrastructure.PostgresSql` | PostgreSQL storage provider | Choose one |
| `Propel.FeatureFlags.Infrastructure.SqlServer` | SQL Server storage provider | Choose one |
| `Propel.FeatureFlags.Infrastructure.Redis` | Redis caching | Optional |
| `Propel.FeatureFlags.Attributes` | AOP-style attribute evaluation | Optional |

## Extensibility

Implement custom storage providers:
```csharp
public class CustomRepository : IFeatureFlagRepository
{
    public Task<FeatureFlag?> GetAsync(string key, FeatureFlagFilter filter, CancellationToken cancellationToken)
    {
        // Your implementation
    }
    // ... other methods
}

// Register
builder.Services.AddScoped<IFeatureFlagRepository, CustomRepository>();
```

## Configuration

```json
{
  "PropelFeatureFlags": {
    "UseCache": true,
    "DefaultTimeZone": "UTC"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=FeatureFlags;...",
    "Redis": "localhost:6379"
  }
}
```

## License

MIT License - see LICENSE file for details.

## Contributing

Contributions welcome! This is an open-source project designed to be extended by the community.