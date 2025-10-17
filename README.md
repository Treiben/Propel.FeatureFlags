# Propel Feature Flags for .NET

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![NuGet](https://img.shields.io/nuget/v/Propel.FeatureFlags.svg)](https://www.nuget.org/packages/Propel.FeatureFlags/)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)

A type-safe feature flag library for .NET that separates continuous delivery from release management. Developers define flags in code, product owners control releases through configuration. Supports modern .NET CORE (6+) applications as well as legacy .NET FULL FRAMEWORK (4.7.2+) applications.

---

## Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Middleware Configuration](#middleware-configuration)
- [Evaluation Modes](#evaluation-modes)
- [Flag Factory Pattern](#flag-factory-pattern)
- [Application vs Global Flags](#application-vs-global-flags)
- [Caching](#caching-options)
- [Working in Legacy Applications](./docs/legacy-dotnet-framework.md)
- [Best Practices](#best-practices)
- [Examples](#examples)
- [Package Reference](#package-reference)
- [Management Tools](#management-tools)
- [Contributing](#contributing)
- [License](#license)
- [Support](#support)

---

## Overview

### The Problem

Traditional feature flag implementations couple developers to release decisions:

```csharp
// Magic strings, no type safety, hard to find during cleanup, is it a business rule or a feature flag (the dev who did this left long time ago...)?
if (config["is_something_enabled"] == "true")
{
    // New implementation
}

// PO wants scheduled release? Developer must change code to add scheduling logic
// PO wants percentage rollout? Developer must implement rollout logic
// PO wants to target specific users? More developer work...
```

This makes developers responsible for release timing, rollout strategies, and configuration management instead of focusing on building features.

### The Solution

Propel separates concerns: developers define flags as strongly-typed classes, product owners configure release strategies through management tools.

**Developer defines the flag once:**

```csharp
public class NewCheckoutFeatureFlag : FeatureFlagBase
{
    public NewCheckoutFeatureFlag()
        : base(key: "new-checkout",
               name: "New Checkout Flow",
               description: "Enhanced checkout with improved UX",
               onOfMode: EvaluationMode.Off) // Deploy disabled by default
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

**Product owner configures release strategy (no developer involvement):**

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

### 1. Configure in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
	.ConfigureFeatureFlags(config =>
		{
			config.RegisterFlagsWithContainer = true;   // automatically register all flags in the assembly with the DI container
			config.EnableFlagFactory = true;            // enable IFeatureFlagFactory for type-safe flag access

			var interception = config.Interception;
			interception.EnableHttpIntercepter = true;  // automatically add interceptors for attribute-based flags
		})
	.AddPostgreSqlFeatureFlags(builder.Configuration.GetConnectionString("DefaultConnection")!)
	.AddRedisCache(builder.Configuration.GetConnectionString("RedisConnection")!,
		options =>
		{
			options.EnableInMemoryCache = true;         // Enable local in-memory cache as first-level cache
			options.CacheDurationInMinutes = 2;         // 2 minutes while in development, increase it to like 3 hours for stable production
			options.LocalCacheSizeLimit = 2000;         // Support more flags
			options.LocalCacheDurationSeconds = 15;     // Slightly longer local cache
			options.CircuitBreakerThreshold = 5;        // More tolerant in production
			options.RedisTimeoutMilliseconds = 7000;    // Longer timeout for remote Redis		
		});  // Configure caching (optional, but recommended for performance and scalability)

// optional: register your services with methods decorated with [FeatureFlagged] attribute
builder.Services.RegisterWithFeatureFlagInterception<INotificationService, NotificationService>();

var app = builder.Build();

// optional: ensure the feature flags database exists and schema is created
if (app.Environment.IsDevelopment())
{
	await app.InitializeFeatureFlagsDatabase();
}

// recommended: automatically add flags in the database at startup if they don't exist
await app.AutoDeployFlags();

// optional:add the feature flag middleware to the pipeline for global flags evaluation and to extract evaluation context from request paths or headers
app.AddFeatureFlagMiddleware("maintenance+headers");

app.Run();
```

### 2. Define a Feature Flag

```csharp
public class NewProductApiFeatureFlag : FeatureFlagBase
{
    public NewProductApiFeatureFlag()
        : base(key: "new-product-api",
               name: "New Product API",
               description: "Enhanced product API with improved performance",
               onOfMode: EvaluationMode.Off) // Start disabled
    {
    }
}
```

### 3. Use the Flag

**Direct evaluation:**

```csharp
app.MapGet("/products", async (HttpContext context) =>
{
    var flag = new NewProductApiFeatureFlag();
    
    // HttpContext extension method for easy evaluation - 
    // If user identity and/or other attributes are needed from the request
    // add middleware to the pipeline to enrich the context with evaluation data

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

### 4. Use Attributes for Clean Code

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

### Database Providers

**PostgreSQL:**

```csharp
using Propel.FeatureFlags.PostgreSql.Extensions;

builder.Services.AddPostgreSqlFeatureFlags(builder.Configuration.GetConnectionString("DefaultConnection")!);

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

builder.Services.AddSqlServerFeatureFlags(builder.Configuration.GetConnectionString("DefaultConnection")!);
```

Both databases support identical functionality. The only difference is in the generated SQL queries.

[⬆ Back to top](#table-of-contents)

### Caching Options

**⚡ Performance Critical:** Feature flag evaluation must be fast enough not to affect application performance. Caching is **optional but highly recommended** for production workloads.

Propel supports **two-level caching** for optimal performance:

#### Two-Level Cache (Recommended for Production)

Best performance with Redis distributed cache + in-memory local cache:

```csharp
	.AddRedisCache(builder.Configuration.GetConnectionString("RedisConnection")!,
		options =>
		{
			options.EnableInMemoryCache = true;         // Enable local in-memory cache as first-level cache
			options.CacheDurationInMinutes = 2;         // 2 minutes while in development, increase it to like 3 hours for stable production
			options.LocalCacheSizeLimit = 2000;         // Support more flags
			options.LocalCacheDurationSeconds = 15;     // Slightly longer local cache
			options.CircuitBreakerThreshold = 5;        // More tolerant in production
			options.RedisTimeoutMilliseconds = 7000;    // Longer timeout for remote Redis		
		});  // Configure caching (optional, but recommended for performance and scalability)
```

**How it works:**
1. **First check**: Local in-memory cache (sub-millisecond)
2. **Second check**: Redis distributed cache (1-2ms)
3. **Final fallback**: Database query (10-50ms)

This provides **microsecond-level performance** for most flag evaluations while keeping flags synchronized across all application instances.

#### Local Cache Only (No Redis)

If Redis is not available in your enterprise environment:

```csharp
builder.Services
	.ConfigureFeatureFlags(config =>
		{
            ...
			config.LocalCacheConfiguration = new LocalCacheConfiguration	// Configure caching (optional, but recommended for performance and scalability)
			{
				LocalCacheEnabled = true,
				CacheDurationInMinutes = 10,								// short local cache duration for better consistency,
				CacheSizeLimit = 1000                                       // limit local cache size to prevent memory bloat
			};
            ...
		});
```

**Trade-offs:**
- ✅ Still provides excellent performance (sub-millisecond)
- ✅ No external dependencies
- ⚠️ Each instance maintains its own cache
- ⚠️ Flag updates may take up to cache duration to propagate across instances
- ⚠️ Not suitable for high-frequency flag changes

**Recommendation:** Use shorter cache durations (1-5 minutes) when Redis is not available to reduce propagation delay.

#### No Cache (Not Recommended)

Only for development or very low-traffic scenarios:

```csharp
builder.Services
	.ConfigureFeatureFlags(config =>
		{
            ...
            // default is disabled local caching but you can also set it explicitly
			config.LocalCacheConfiguration = new LocalCacheConfiguration
			{
				LocalCacheEnabled = false, // set it to false or don't setup at all
			};
            ...
		});
```

**Warning:** Every flag evaluation hits the database. This can significantly impact performance under load.

[⬆ Back to top](#table-of-contents)

## Middleware Configuration

The middleware handles global flags, maintenance mode, and extracts context (user ID, attributes) from HTTP requests.

```csharp
app.UseFeatureFlags(); // Default configuration
```

### Enable Global Kill Switch

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

### Extract User Identity

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

### Extract Custom Attributes

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

### Combined Configuration

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

### 1. Always Deploy Disabled

Always set `onOfMode: EvaluationMode.Off` when defining new flags. This allows you to deploy code without immediately releasing the feature.

```csharp
public class NewFeatureFlag : FeatureFlagBase
{
    public NewFeatureFlag()
        : base(key: "new-feature",
               name: "New Feature",
               description: "Description",
               onOfMode: EvaluationMode.Off) // ✅ Deploy first, release later
    {
    }
}
```

This separates deployment from release. Developers deploy the code, product owners control when it's released by enabling the flag in the dashboard.

### 2. Feature Flags Control "How", Not "What"

Feature flags control how your application behaves technically, not what it does from a business perspective.

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
    return CalculatePricingV2(); // New algorithm
}
return CalculatePricingV1(); // Old algorithm

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

### 3. Clean Up Old Flags

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
return NewCheckout(); // New implementation is now the standard

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

### 5. Safety Mechanism: Auto-Recreation

Propel automatically recreates flags that are missing from the database. This prevents production errors if a flag is accidentally deleted from the database.

If a flag is evaluated but doesn't exist in the database:

1. Propel creates it with the default state from code (`onOfMode`)
2. The evaluation continues using the default state
3. Product owners can then configure the flag in the dashboard

This safety mechanism ensures your application never crashes due to missing flags.

[⬆ Back to top](#table-of-contents)

## Examples

Complete working examples are available in the `/usage-demo` directory:

### [WebClientDemo](./usage-demo/DemoWebApi)

ASP.NET Core Web API demonstrating:
- Simple on/off flags
- Scheduled releases
- Time window flags
- Percentage rollouts with variations
- User targeting with custom attributes
- Attribute-based flags with interceptors for minimal api http calls

### [ConsoleAppDemo](./usage-demo/DemoWorker)

Console worker application demonstrating:
- Background service integration
- Attribute-based flags without HTTP context
- Direct flag evaluation via `IApplicationFlagClient`
- Using `IFeatureFlagFactory` for type-safe access
- Attribute-based flags with interceptors for non-http calls

### [Legacy .NET Framework](./usage-demo/DemoLegacyApi)

Working with full .NET Framework applications ([documentation](./docs/legacy-dotnet-framework.md))

[⬆ Back to top](#table-of-contents)

## Package Reference

| Package | Purpose | Target Framework |
|---------|---------|------------------|
| `Propel.FeatureFlags` | Core library and interfaces | .NET Standard 2.0 |
| `Propel.FeatureFlags.AspNetCore` | ASP.NET Core middleware and extensions | .NET Standard 2.0 |
| `Propel.FeatureFlags.DependencyInjection.Extensions` | Advanced DI registration helpers | .NET Standard 2.0 |
| `Propel.FeatureFlags.PostgresSql` | PostgreSQL persistence provider | .NET 9.0 |
| `Propel.FeatureFlags.SqlServer` | SQL Server persistence provider | .NET 9.0 |
| `Propel.FeatureFlags.Redis` | Redis distributed caching | .NET Standard 2.0 |
| `Propel.FeatureFlags.Attributes` | AOP-style method attributes | .NET Standard 2.0 |

[⬆ Back to top](#table-of-contents)

## Management Tools

Propel provides multiple tools for managing feature flags:

### 🎯 [Propel Dashboard](https://github.com/Treiben/propel-dashboard)

Web-based management interface for product owners and DevOps teams:

- **Visual Flag Management** - View all application flags deployed from code
- **Release Configuration** - Configure evaluation modes (scheduled, time windows, targeting, rollouts)
- **Targeting Rules** - Set up complex targeting based on custom attributes
- **Rollout Controls** - Percentage-based rollouts for gradual feature releases
- **Global Flags** - Manage system-wide flags (maintenance mode, kill switches)
- **Monitoring** - Track flag usage and expiration dates
- **User Management** - Role-based access control

**Quick Start:**
```bash
docker run -d \
  -p 8080:8080 \
  -e SQL_CONNECTION="Host=your-postgres;Database=propel;..." \
  propel/feature-flags-dashboard:latest
```

Access at `http://localhost:8080` (default credentials: `admin` / `Admin123!`)

### 🔧 [Propel CLI](https://github.com/Treiben/propel-cli)

Command-line tool for automation and CI/CD pipelines:

- **Database Migrations** - Run schema migrations in pipelines
- **Flag Management** - Create, update, and delete flags from terminal
- **Configuration Export/Import** - Backup and restore flag configurations
- **Bulk Operations** - Manage multiple flags at once
- **Pipeline Integration** - Perfect for GitOps workflows

**Installation:**
```bash
dotnet tool install -g Propel.FeatureFlags.Cli
```

**Common Commands:**
```bash
# Run migrations
propel migrate --connection "Host=postgres;..."

# List all flags
propel flags list

# Enable a flag
propel flags enable --key new-checkout

# Export configuration
propel export --output flags-backup.json
```

### Which Tool to Use?

| Scenario | Recommended Tool |
|----------|------------------|
| Product owners configuring releases | Dashboard |
| DevOps setting up maintenance windows | Dashboard |
| CI/CD pipeline migrations | CLI |
| Automated flag management | CLI |
| Manual testing and verification | Dashboard |
| Bulk configuration changes | CLI |

[⬆ Back to top](#table-of-contents)

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines.

---

## License

Apache-2.0 License - see [LICENSE](./LICENSE) file for details.

---

## Support

- **Issues**: [GitHub Issues](https://github.com/Treiben/propel-feature-flags-csharp/issues)
- **Examples**: [usage-demo/](./usage-demo)
- **Dashboard**: [propel-dashboard](https://github.com/Treiben/propel-dashboard)
- **CLI**: [propel-cli](https://github.com/Treiben/propel-cli)

---

**Built with ❤️ for .NET developers by Tatyana Asriyan**

[⬆ Back to top](#table-of-contents)