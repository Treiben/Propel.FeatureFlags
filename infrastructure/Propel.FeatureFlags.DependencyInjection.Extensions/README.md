## Propel.FeatureFlags.DependencyInjection.Extensions
[![Build and Test](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Propel.FeatureFlags.DependencyInjection.Extensions.svg)](https://www.nuget.org/packages/Propel.FeatureFlags.DependencyInjection.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Propel.FeatureFlags.DependencyInjection.Extensions.svg?style=flat-square)](https://www.nuget.org/packages/Propel.FeatureFlags.DependencyInjection.Extensions/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-standard-versions)

Extension methods for configuring and integrating Propel.FeatureFlags with .NET applications using dependency injection.

For detailed documentation and examples, visit the repository [readme](../../README.md).

### Features
-	`IServiceCollection` Extensions - Configure feature flags both in ASP.NET Core and console applications
-	Automatic Flag Registration - Automatically discovers and registers feature flags from assemblies
-	Auto-Deployment - Automatically creates flags in the database on application startup
-	Type-Safe Flag Factory - Provides `IFeatureFlagFactory` for compile-time safe flag access
-	Local Caching - Built-in memory cache configuration for better performance
-	Attribute-Based Interception - Support for `[FeatureFlagged]` attribute on methods
-	HTTP Context Integration - Integration with ASP.NET Core middleware pipeline
  
### Installation
```bash
dotnet add package Propel.FeatureFlags.DependencyInjection.Extensions
```

### Quick Start

#### ASP.NET Core Web API
```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure Propel FeatureFlags
builder.Services
    .ConfigureFeatureFlags(config =>
    {
        config.RegisterFlagsWithContainer = true;   // Auto-register flags with DI
        config.EnableFlagFactory = true;            // Enable type-safe flag access
        
        // Enable attribute-based interception for HTTP controllers
        config.Interception.EnableHttpIntercepter = true;
        
        // Optional: Configure local caching
        config.LocalCacheConfiguration = new LocalCacheConfiguration
        {
            LocalCacheEnabled = true,
            CacheDurationInMinutes = 60,
            CacheSizeLimit = 1000
        };
    })
    .AddPostgreSqlFeatureFlags(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

// Initialize database (development only)
if (app.Environment.IsDevelopment())
{
    await app.InitializeFeatureFlagsDatabase();
}

// Auto-deploy flags to database
await app.AutoDeployFlags();

app.Run();
```

#### Console Application / Worker Service
```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .ConfigureFeatureFlags(config =>
    {
        config.RegisterFlagsWithContainer = true;
        config.EnableFlagFactory = true;
        
        // For console apps, use EnableIntercepter (not EnableHttpIntercepter)
        config.Interception.EnableIntercepter = true;
        
        config.LocalCacheConfiguration = new LocalCacheConfiguration
        {
            LocalCacheEnabled = true,
            CacheDurationInMinutes = 10,
            CacheSizeLimit = 1000
        };
    })
    .AddSqlServerFeatureFlags(builder.Configuration.GetConnectionString("DefaultConnection")!);

// Register services with attribute interception
builder.Services.RegisterWithFeatureFlagInterception<INotificationService, NotificationService>();

var app = builder.Build();

// Auto-deploy flags
await app.AutoDeployFlags();

await app.RunAsync();
```

#### Configuration Options
`PropelConfiguration`

| Property | Description | Default | 
|----------|-------------|---------| 
| RegisterFlagsWithContainer | Automatically register all flags in assembly with DI | true | 
| EnableFlagFactory | Enable IFeatureFlagFactory for type-safe access | true | 
| AutoDeployFlags | Automatically deploy flags on startup | false | 
| LocalCacheConfiguration | Configure local memory cache settings | See below | 
| Interception.EnableIntercepter | Enable attribute interception for console apps | false | 
| Interception.EnableHttpIntercepter | Enable attribute interception for ASP.NET Core | false |


`LocalCacheConfiguration`

| Property | Description | Default | 
|----------|-------------|---------| 
| LocalCacheEnabled | Enable local in-memory caching | false | 
| CacheDurationInMinutes | Cache expiration time (minutes) | 60 | 
| CacheSizeLimit | Maximum number of cached flags | 1000 |

#### Extension Methods

| Method | Description | Example |
|--------|-------------|---------|
| ConfigureFeatureFlags(Action<PropelConfiguration>) | Configures core feature flag services with the DI container. | See below |
| AutoDeployFlags() | Automatically creates feature flags in the database if they don't exist. Recommended to call on application startup. | See below |
| InitializeFeatureFlagsDatabase() | Creates the database schema for feature flags. Typically used in development environments. | See below |
| RegisterWithFeatureFlagInterception<TInterface, TImplementation>() | Registers a service with support for [FeatureFlagged] attribute interception. | See below |

```csharp
// Deploy feature flags database schema
if (app.Environment.IsDevelopment())
{
    await app.InitializeFeatureFlagsDatabase();
}
```

```csharp
// Register a service with attribute-based feature flag interception
builder.Services.RegisterWithFeatureFlagInterception<INotificationService, NotificationService>();
```

#### Attribute-Based Feature Flagging

Define a service with feature-flagged methods:
```csharp
public class NotificationService : INotificationService
{
    [FeatureFlagged(type: typeof(NewEmailServiceFeatureFlag), fallbackMethod: nameof(SendEmailLegacyAsync))]
    public virtual async Task<string> SendEmailAsync(string userId, string subject, string body)
    {
        // New implementation
        return "Email sent using NEW service";
    }

    public virtual async Task<string> SendEmailLegacyAsync(string userId, string subject, string body)
    {
        // Legacy fallback
        return "Email sent using LEGACY service";
    }
}
```

Register the service:
```csharp
// Console apps
builder.Services.RegisterWithFeatureFlagInterception<INotificationService, NotificationService>();

// ASP.NET Core - automatically works with EnableHttpIntercepter = true
builder.Services.AddScoped<INotificationService, NotificationService>();
```

#### Storage Providers

This library works with various storage providers:
```csharp
// SQL Server
.AddSqlServerFeatureFlags(connectionString)

// PostgreSQL
.AddPostgreSqlFeatureFlags(connectionString)

// Add Redis caching layer (optional)
.AddRedisCache(redisConnectionString, options => { ... })
;
```

### Complete Example

See the [demo](../../usage-demo) projects for complete working examples:
-	DemoWebApi - ASP.NET Core Web API with middleware, controllers, and minimal APIs
-	DemoWorker - .NET Core Console application / Background Worker service
-	DemoLegacyApi - .NET Framework 4.8.1 compatibility

### Best Practices

1.	Always call `AutoDeployFlags()` on startup to ensure flags exist in the database
2.	Enable caching in production for better performance and reduced database load
3.	Use `InitializeFeatureFlagsDatabase()` only in development environments
4.	Register flags with DI container (`RegisterFlagsWithContainer = true`) for easier testing
5.	Enable `IFeatureFlagFactory` for type-safe flag access throughout your application
 
### Requirements

-	.NET Standard 2.0+ (core library)
-	.NET 6.0+ (for ASP.NET Core integration)
-	Compatible with .NET Framework 4.8.1+

### Related Packages

-	[Propel.FeatureFlags](../../src/Propel.FeatureFlags/) - Core feature flag library    
-	[Propel.FeatureFlags.SqlServer](../Propel.FeatureFlags.SqlServer/) - SQL Server repository
-	[Propel.FeatureFlags.PostgreSql](../Propel.FeatureFlags.PostgreSql/) - PostgreSQL repository
-	[Propel.FeatureFlags.Redis](../Propel.FeatureFlags.Redis/) - Redis caching provider
-	[Propel.FeatureFlags.AspNetCore](../../src/Propel.FeatureFlags.AspNetCore/) - ASP.NET Core middleware
-	[Propel.FeatureFlags.Attributes](../../src/Propel.FeatureFlags.Attributes/) - Attribute-based feature flagging