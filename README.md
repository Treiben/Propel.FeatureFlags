# Propel.FeatureFlags
[![Build and Test](https://github.com/Treiben/Propel.FeatureFlags/actions/workflows/build.yml/badge.svg)](https://github.com/Treiben/Propel.FeatureFlags/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Propel.FeatureFlags.svg)](https://www.nuget.org/packages/Propel.FeatureFlags)
![.NET Standard](https://img.shields.io/badge/.NET%20Standard-2.0-5C2D91?logo=.net)
![.NET Core](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)


A type-safe feature flag library for .NET that separates continuous delivery from release management. Developers define flags in code, product owners control releases through configuration.

## Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Middleware Configuration](#middleware-configuration)
- [Evaluation Modes](#evaluation-modes)
- [Flag Factory Pattern](#flag-factory-pattern)
- [Application vs Global Flags](#application-vs-global-flags)
- [Working in legacy application](docs/legacy-dotnet-framework.md)
- [Best Practices](#best-practices)
- [Examples](#examples)
- [Package Reference](#package-reference)
- [Management Dashboard](#management-dashboard)
- [Contributing](#contributing)
- [License](#license)
- [Support](#support)

## Overview
### The Problem

Traditional feature flag implementations couple developers to release decisions:

```csharp
// Magic strings, no type safety, hard to find during cleanup
if (config["new-feature"] == "true") 
{
    // New implementation
}

// PO wants scheduled release? Developer must change code to add scheduling logic
// PO wants percentage rollout? Developer must implement rollout logic
// PO wants to target specific users? More developer work...
```

This makes developers responsible for release timing, rollout strategies, and configuration management instead of focusing on building features.

### The Solution

Propel separates concerns: developers define flags as strongly-typed classes, product owners configure release strategies through a management dashboard.

**Developer defines the flag once:**
```csharp
public class NewCheckoutFeatureFlag : FeatureFlagBase
{
    public NewCheckoutFeatureFlag() 
        : base(key: "new-checkout",
               name: "New Checkout Flow",
               description: "Enhanced checkout with improved UX",
               onOfMode: EvaluationMode.Off)  // Deploy disabled by default
    {
    }
}
```

**Developer uses the flag in code:**
```csharp
var flag = new NewCheckoutFeatureFlag();
if (await context.IsFeatureFlagEnabledAsync(flag))
{
    return NewCheckoutImplementation();
}
return LegacyCheckoutImplementation();
```

**Product owner configures release strategy** (no developer involvement):
- Schedule: Enable feature on specific date/time
- Time windows: Enable only during business hours
- User targeting: Enable for specific users or user groups
- Percentage rollout: Gradually roll out to 10%, 25%, 50%, 100% of users
- Targeting rules: Enable based on custom attributes (region, tier, department, etc.)

### Key Benefits

- **Type Safety**: Compile-time validation prevents runtime errors from typos or missing flags
- **Easy Maintenance**: Find-all-references works; no magic strings scattered across config files
- **Clean Code Hygiene**: Delete the flag class when done, compiler tells you everywhere it was used
- **Attribute-Based Flags**: Decorate methods with `[FeatureFlagged]` to keep your business logic clean
- **Auto-Deployment**: Flags automatically register in database on application startup
- **Zero Configuration**: Works out of the box with sensible defaults

[⬆ Back to top](#table-of-contents)

## Installation

```bash
# Core library (required)
dotnet add package Propel.FeatureFlags

# ASP.NET Core integration
dotnet add package Propel.FeatureFlags.AspNetCore

# Database persistence (choose one)
dotnet add package Propel.FeatureFlags.Infrastructure.PostgresSql
dotnet add package Propel.FeatureFlags.Infrastructure.SqlServer

# Caching (optional but recommended)
dotnet add package Propel.FeatureFlags.Infrastructure.Redis

# AOP-style attributes (optional)
dotnet add package Propel.FeatureFlags.Attributes
```

[⬆ Back to top](#table-of-contents)

## Quick Start

### 1. Configure Services

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.ConfigureFeatureFlags(config =>
{
    config.RegisterFlagsWithContainer = true;    // Auto-register all flags in DI
    config.EnableFlagFactory = true;             // Enable type-safe flag retrieval
    
    config.SqlConnection = builder.Configuration
        .GetConnectionString("DefaultConnection")!;
    
    config.Cache = new CacheOptions 
    {
        EnableInMemoryCache = true,
        CacheDurationInMinutes = TimeSpan.FromMinutes(30),
        SlidingDurationInMinutes = TimeSpan.FromMinutes(10)
    };
    
    // For ASP.NET Core apps with HTTP context-based targeting
    config.Interception.EnableHttpIntercepter = true;
    
    // For console apps or non-HTTP scenarios
    // config.Interception.EnableIntercepter = true;
})
.AddRedisCache(builder.Configuration.GetConnectionString("RedisConnection")!);

var app = builder.Build();

// Initialize database (development only - use migrations in production)
if (app.Environment.IsDevelopment())
{
    await app.InitializeFeatureFlagsDatabase();
}

// Auto-deploy all flags defined in code
await app.AutoDeployFlags();

// Add middleware for global flag evaluation and context extraction
app.UseFeatureFlags();

app.Run();
```

### 2. Define Feature Flags

```csharp
public class NewProductApiFeatureFlag : FeatureFlagBase
{
    public NewProductApiFeatureFlag()
        : base(key: "new-product-api",
               name: "New Product API",
               description: "Enhanced product API with improved performance",
               onOfMode: EvaluationMode.Off)  // Start disabled
    {
    }
}
```

### 3. Evaluate Flags

**Direct evaluation:**
```csharp
app.MapGet("/products", async (HttpContext context) =>
{
    var flag = new NewProductApiFeatureFlag();
    
    if (await context.IsFeatureFlagEnabledAsync(flag))
    {
        return Results.Ok(GetProductsV2());
    }
    
    return Results.Ok(GetProductsV1());
});
```

**Using factory pattern:**
```csharp
app.MapGet("/products", async (HttpContext context, IFeatureFlagFactory factory) =>
{
    var flag = factory.GetFlagByType<NewProductApiFeatureFlag>();
    
    if (await context.IsFeatureFlagEnabledAsync(flag))
    {
        return Results.Ok(GetProductsV2());
    }
    
    return Results.Ok(GetProductsV1());
});
```

**Getting variations:**
```csharp
app.MapGet("/recommendations/{userId}", async (HttpContext context) =>
{
    var flag = new RecommendationAlgorithmFeatureFlag();
    var algorithm = await context.GetFeatureFlagVariationAsync(flag, "collaborative-filtering");
    
    return algorithm switch
    {
        "machine-learning" => GetMLRecommendations(userId),
        "content-based" => GetContentBasedRecommendations(userId),
        _ => GetCollaborativeRecommendations(userId)
    };
});
```

### 4. Attribute-Based Flags (Optional)

For the cleanest code, use attributes to automatically call fallback methods when flags are disabled:

```csharp
// Register service with interceptor support
builder.Services.RegisterWithFeatureFlagInterception<INotificationService, NotificationService>();

public interface INotificationService
{
    Task<string> SendEmailAsync(string userId, string subject, string body);
    Task<string> SendEmailLegacyAsync(string userId, string subject, string body);
}

public class NotificationService : INotificationService
{
    [FeatureFlagged(type: typeof(NewEmailServiceFeatureFlag), 
                     fallbackMethod: nameof(SendEmailLegacyAsync))]
    public virtual async Task<string> SendEmailAsync(string userId, string subject, string body)
    {
        // New implementation - called when flag is enabled
        return "Email sent using new service";
    }
    
    public virtual async Task<string> SendEmailLegacyAsync(string userId, string subject, string body)
    {
        // Fallback - called when flag is disabled
        return "Email sent using legacy service";
    }
}
```

[⬆ Back to top](#table-of-contents)

## Configuration

### PropelConfiguration Options

```csharp
builder.ConfigureFeatureFlags(config =>
{
    // Auto-register all IFeatureFlag implementations in DI container
    config.RegisterFlagsWithContainer = true;
    
    // Enable IFeatureFlagFactory for type-safe flag access
    config.EnableFlagFactory = true;
    
    // Database connection string
    config.SqlConnection = "Host=localhost;Database=propel;...";
    
    // Default timezone for scheduled flags and time windows
    config.DefaultTimeZone = "UTC";
    
    // Caching configuration
    config.Cache = new CacheOptions
    {
        EnableInMemoryCache = true,           // Per-instance cache
        EnableDistributedCache = false,       // Requires Redis
        CacheDurationInMinutes = TimeSpan.FromMinutes(30),
        SlidingDurationInMinutes = TimeSpan.FromMinutes(10)
    };
    
    // AOP interceptor configuration
    config.Interception = new AOPOptions
    {
        EnableHttpIntercepter = true,   // For ASP.NET Core apps
        EnableIntercepter = false       // For console apps
    };
});
```

### Database Setup

**PostgreSQL:**
```csharp
builder.ConfigureFeatureFlags(config =>
{
    config.SqlConnection = builder.Configuration
        .GetConnectionString("DefaultConnection")!;
});

// In development, auto-initialize database
if (app.Environment.IsDevelopment())
{
    await app.InitializeFeatureFlagsDatabase();
}

// Optional: seed with SQL script
// await app.Services.SeedFeatureFlags("seed-db.sql");
```

**SQL Server:**
```csharp
using Propel.FeatureFlags.SqlServer.Extensions;

builder.ConfigureFeatureFlags(config =>
{
    config.SqlConnection = builder.Configuration
        .GetConnectionString("DefaultConnection")!;
});
```

Both databases support identical functionality. The only difference is in the generated SQL queries.

### Caching

**In-Memory Cache** (single instance, fastest):
```csharp
config.Cache = new CacheOptions
{
    EnableInMemoryCache = true
};
```

**Distributed Cache** (Redis, shared across instances):
```csharp
builder.ConfigureFeatureFlags(config => { ... })
    .AddRedisCache("localhost:6379");

config.Cache = new CacheOptions
{
    EnableDistributedCache = true,
    CacheDurationInMinutes = TimeSpan.FromMinutes(30),
    SlidingDurationInMinutes = TimeSpan.FromMinutes(10)
};
```

**No Cache** (not recommended for production):
```csharp
config.Cache = new CacheOptions
{
    EnableInMemoryCache = false,
    EnableDistributedCache = false
};
```

[⬆ Back to top](#table-of-contents)

## Middleware Configuration

The middleware handles global flags, maintenance mode, and extracts context (user ID, attributes) from HTTP requests.

### Basic Configuration

```csharp
app.UseFeatureFlags();  // Default configuration
```

### Maintenance Mode

Enable global kill switch for the entire API:

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

When the `api-maintenance` global flag is enabled, all requests return 503 with the configured response.

### Custom User ID Extraction

Extract user identity from JWT tokens, API keys, or headers:

```csharp
app.UseFeatureFlags(options =>
{
    options.UserIdExtractor = context =>
    {
        // Try JWT sub claim
        var jwtUserId = context.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(jwtUserId)) return jwtUserId;
        
        // Try API key
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey)) return $"api:{apiKey}";
        
        // Try session ID
        return context.Request.Headers["X-Session-ID"].FirstOrDefault();
    };
});
```

### Attribute Extractors for Targeting Rules

Extract attributes from requests to enable complex targeting:

```csharp
app.UseFeatureFlags(options =>
{
    options.AttributeExtractors.Add(context =>
    {
        var attributes = new Dictionary<string, object>();
        
        // Extract tenant information
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId))
            attributes["tenantId"] = tenantId.ToString();
        
        // Extract user tier from JWT
        var userTier = context.User.FindFirst("tier")?.Value;
        if (!string.IsNullOrEmpty(userTier))
            attributes["userTier"] = userTier;
        
        // Extract geographic info
        if (context.Request.Headers.TryGetValue("Country", out var country))
            attributes["country"] = country.ToString();
        
        return attributes;
    });
});
```

### Complete Example

Combine maintenance mode with attribute extraction:

```csharp
app.UseFeatureFlags(options =>
{
    // Enable maintenance mode
    options.EnableMaintenanceMode = true;
    options.MaintenanceFlagKey = "api-maintenance";
    
    // Custom user extraction
    options.UserIdExtractor = context =>
    {
        return context.User.Identity?.Name ??
               context.Request.Headers["User-Id"].FirstOrDefault();
    };
    
    // Extract targeting attributes
    options.AttributeExtractors.Add(context =>
    {
        var attributes = new Dictionary<string, object>();
        
        if (context.Request.Headers.TryGetValue("Role", out var role))
            attributes["role"] = role.ToString();
        
        if (context.Request.Headers.TryGetValue("Department", out var dept))
            attributes["department"] = dept.ToString();
        
        return attributes;
    });
});
```

[⬆ Back to top](#table-of-contents)

## Evaluation Modes

Propel supports 9 evaluation modes that can be configured from the management dashboard:

| Mode | Description | Use Case |
|------|-------------|----------|
| `Off` | Flag is disabled | Default safe state, kill switches |
| `On` | Flag is enabled | Enable completed features |
| `Scheduled` | Time-based activation | Coordinated releases, marketing campaigns |
| `TimeWindow` | Daily/weekly time ranges | Business hours features, maintenance windows |
| `UserTargeted` | Specific user allowlists/blocklists | Beta testing, VIP access |
| `UserRolloutPercentage` | Percentage-based user rollout | Gradual rollouts, A/B testing |
| `TenantRolloutPercentage` | Percentage-based tenant rollout | Multi-tenant gradual rollouts |
| `TenantTargeted` | Specific tenant allowlists/blocklists | Tenant-specific features |
| `TargetingRules` | Custom attribute-based rules | Complex targeting (region, tier, etc.) |

**Note:** Developers define flags with `OnOffMode` set to either `On` or `Off`. All other evaluation modes are configured by product owners through the management dashboard.

[⬆ Back to top](#table-of-contents)

## Flag Factory Pattern

For centralized flag management in larger codebases:

```csharp
public interface IFeatureFlagFactory
{
    IFeatureFlag? GetFlagByKey(string key);
    IFeatureFlag? GetFlagByType<T>() where T : IFeatureFlag;
    IEnumerable<IFeatureFlag> GetAllFlags();
}

// Automatically registered when EnableFlagFactory = true
app.MapGet("/products", async (IFeatureFlagFactory factory, HttpContext context) =>
{
    var flag = factory.GetFlagByType<NewProductApiFeatureFlag>();
    
    if (await context.IsFeatureFlagEnabledAsync(flag))
    {
        return Results.Ok("New API");
    }
    
    return Results.Ok("Legacy API");
});
```

[⬆ Back to top](#table-of-contents)

## Application vs Global Flags

### Application Flags

Application flags are defined in code and scoped to specific applications. They auto-deploy on startup.

```csharp
public class MyFeatureFlag : FeatureFlagBase
{
    public MyFeatureFlag() 
        : base(key: "my-feature", 
               name: "My Feature",
               description: "Description of my feature",
               onOfMode: EvaluationMode.Off)
    {
    }
}

// Usage
var flag = new MyFeatureFlag();
var isEnabled = await context.IsFeatureFlagEnabledAsync(flag);
```

### Global Flags

Global flags are created through the management dashboard and apply system-wide across all applications. Use them for:

- Maintenance mode
- API deprecation
- System-wide kill switches

```csharp
// Global flags are evaluated by key only
var isEnabled = await globalFlagClient.IsEnabledAsync("api-maintenance");
```

**Important:** Global flags cannot be defined in code. They must be created through the dashboard.

[⬆ Back to top](#table-of-contents)

## Best Practices

### 1. Default to Disabled

Always set `onOfMode: EvaluationMode.Off` when defining new flags. This allows you to deploy code without immediately releasing the feature.

```csharp
public class NewFeatureFlag : FeatureFlagBase
{
    public NewFeatureFlag()
        : base(key: "new-feature",
               name: "New Feature",
               description: "Description",
               onOfMode: EvaluationMode.Off)  // ✅ Deploy first, release later
    {
    }
}
```

This separates deployment from release. Developers deploy the code, product owners control when it's released by enabling the flag in the dashboard.

### 2. Never Use Feature Flags for Business Logic

Feature flags control *how* your application behaves technically, not *what* it does from a business perspective.

**❌ Wrong - Business Logic:**
```csharp
// DON'T use flags to control which users have access to premium features
if (await IsFeatureFlagEnabled("premium-features"))
{
    return GetPremiumContent();
}

// DON'T use flags for pricing tiers
if (await IsFeatureFlagEnabled("enterprise-pricing"))
{
    return CalculateEnterprisePricing();
}

// DON'T use flags for user permissions
if (await IsFeatureFlagEnabled("admin-access"))
{
    return AdminDashboard();
}
```

**✅ Correct - Technical Implementation:**
```csharp
// DO use flags to test new technical implementations
if (await IsFeatureFlagEnabled("new-pricing-algorithm"))
{
    return CalculatePricingV2();  // New algorithm
}
return CalculatePricingV1();      // Old algorithm

// DO use flags for infrastructure changes
if (await IsFeatureFlagEnabled("new-email-service"))
{
    return SendEmailViaSendGrid();
}
return SendEmailViaLegacySmtp();

// DO use flags for UI component changes
if (await IsFeatureFlagEnabled("new-checkout-ui"))
{
    return RenderNewCheckoutComponent();
}
return RenderLegacyCheckoutComponent();
```

**Why this matters:** Business logic belongs in your domain layer and should be driven by data (user roles, subscription tiers, entitlements). Feature flags are for deployment safety, gradual rollouts, and A/B testing technical implementations.

### 3. Clean Up Expired Flags

Feature flags are temporary. Delete them when:

- The feature has been fully rolled out to all users
- You've decided to keep one implementation and remove the other
- The flag has expired (check the management dashboard for expiration dates)

**Cleanup process:**

1. Delete the flag class from your codebase
2. Remove all references (compiler will help you find them)
3. Delete the flag from the database through the management dashboard

**Important:** If you only delete from the database, the flag will be auto-created again on the next deployment. Always delete from code first.

```csharp
// After rollout is complete and you've decided to keep v2
// 1. Delete this class
public class NewCheckoutFeatureFlag : FeatureFlagBase { ... }

// 2. Replace conditional code with the new implementation
// Before:
if (await context.IsFeatureFlagEnabledAsync(new NewCheckoutFeatureFlag()))
{
    return NewCheckout();
}
return LegacyCheckout();

// After:
return NewCheckout();  // New implementation is now the standard

// 3. Delete flag from database via dashboard
```

### 4. Use Descriptive Names and Descriptions

Good flag definitions help everyone understand the flag's purpose:

```csharp
public class EnhancedRecommendationEngineFeatureFlag : FeatureFlagBase
{
    public EnhancedRecommendationEngineFeatureFlag()
        : base(
            key: "enhanced-recommendation-engine",
            name: "Enhanced Recommendation Engine",
            description: "Enables ML-based recommendation engine with improved accuracy. " +
                        "Falls back to collaborative filtering if disabled. " +
                        "Safe to roll out gradually to measure performance impact.",
            onOfMode: EvaluationMode.Off)
    {
    }
}
```

### 5. Safety Through Auto-Recreation

Propel automatically recreates flags that are missing from the database. This prevents production errors if a flag is accidentally deleted from the database.

If a flag is evaluated but doesn't exist in the database:
1. Propel creates it with the default state from code (`onOfMode`)
2. The evaluation continues using the default state
3. Product owners can then configure the flag in the dashboard

This safety mechanism ensures your application never crashes due to missing flags.

[⬆ Back to top](#table-of-contents)

## Examples

Complete working examples are available in the `/usage-demo` directory:

- **[WebClientDemo](usage-demo/DemoWebApi)** - ASP.NET Core Web API demonstrating:
  - Simple on/off flags
  - Scheduled releases
  - Time window flags
  - Percentage rollouts with variations
  - User targeting with custom attributes
  - Attribute-based flags with interceptors

- **[ConsoleAppDemo](usage-demo/DemoWorker)** - Console worker application demonstrating:
  - Background service integration
  - Attribute-based flags without HTTP context
  - Direct flag evaluation via `IApplicationFlagClient`
  - Using `IFeatureFlagFactory` for type-safe access

- **[Legacy .NET Framework](usage-demo/DemoLegacyApi)** - Working with full .NET Framework applications ([documentation](docs/legacy-dotnet-framework.md))

[⬆ Back to top](#table-of-contents)

## Package Reference

| Package | Purpose | Target Framework |
|---------|---------|------------------|
| `Propel.FeatureFlags` | Core library and interfaces | .NET Standard 2.0 |
| `Propel.FeatureFlags.AspNetCore` | ASP.NET Core middleware and extensions | .NET 9.0 |
| `Propel.FeatureFlags.Infrastructure.PostgresSql` | PostgreSQL persistence | .NET 9.0 |
| `Propel.FeatureFlags.Infrastructure.SqlServer` | SQL Server persistence | .NET 9.0 |
| `Propel.FeatureFlags.Infrastructure.Redis` | Redis distributed caching | .NET Standard 2.0 |
| `Propel.FeatureFlags.Attributes` | AOP-style method attributes | .NET 9.0 |

[⬆ Back to top](#table-of-contents)

## Management Dashboard

The [Propel.FeatureFlags.Dashboard](https://github.com/Treiben/Propel.FeatureFlags.Dashboard) provides a web interface for product owners to:

- View all application flags deployed from code
- Configure evaluation modes (scheduled, time windows, targeting, rollouts)
- Set up targeting rules based on custom attributes
- Monitor flag usage and expiration dates
- Manage global flags for system-wide concerns

The dashboard is under development and will be released separately.

[⬆ Back to top](#table-of-contents)

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

[⬆ Back to top](#table-of-contents)

## License

Apache-2.0 License - see [LICENSE](LICENSE) file for details.

[⬆ Back to top](#table-of-contents)

## Support

- **Issues**: [GitHub Issues](https://github.com/Treiben/Propel.FeatureFlags/issues)
- **Documentation**: [Wiki](https://github.com/Treiben/Propel.FeatureFlags/wiki)
- **Examples**: [usage-demo/](usage-demo/)

[⬆ Back to top](#table-of-contents)