# Propel.FeatureFlags.AspNetCore
[![Build and Test](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Propel.FeatureFlags.AspNetCore.svg)](https://www.nuget.org/packages/Propel.FeatureFlags.AspNetcore/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Propel.FeatureFlags.AspNetCore.svg?style=flat-square)](https://www.nuget.org/packages/Propel.FeatureFlags.AspNetCore/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-standard-versions)

ASP.NET Core integration library for Propel Feature Flags, providing middleware, extension methods, and HTTP context integration for feature flag evaluation in web applications.

For detailed documentation and examples, visit the repository [readme](../../README.md).

## Features

- **Feature Flag Middleware** - Global feature gates, maintenance mode, and automatic context extraction
- **HTTP Context Extensions** - Evaluate feature flags directly from `HttpContext` or `ControllerBase`
- **Context-Aware Evaluation** - Automatic extraction of tenant ID, user ID, and custom attributes from HTTP requests
- **Fluent Configuration** - Builder pattern for middleware options

## Installation
```bash
dotnet add package Propel.FeatureFlags.AspNetCore
```

## Quick Start

### 1. Configure Services
```csharp
builder.Services 
    .ConfigureFeatureFlags(config => 
        { 
            config.RegisterFlagsWithContainer = true; 
            config.EnableFlagFactory = true;
         }) 
    .AddPostgreSqlFeatureFlags(connectionString) //database provider required
    .AddRedisCache(redisConnection)              //cache provider is optional;
```

### 2. Add Middleware
Basic usage, maintenance mode, and global feature gates examples:
```csharp
// Basic usage 
app.UseFeatureFlags();

```
```csharp
// With maintenance mode 
app.UseFeatureFlags(options => 
    { 
        options
        .EnableMaintenanceMode("api-maintenance") 
        .WithMaintenanceResponse(new { message = "API is temporarily down for maintenance", estimatedDuration = "30 minutes" }); 
    });
```
```csharp
// With global feature gates 
app.UseFeatureFlags(options => 
    { 
        options.AddGlobalFlag("api-v2-enabled", 410, new { error = "API v2 is no longer available" }); 
    });
```

### 3. Evaluate Flags in Endpoints

**Minimal API:**

```csharp
app.MapGet("/admin/sensitive-operation", 
    async (HttpContext context) => 
        { 
            var flag = new AdminPanelEnabledFeatureFlag();
            if (await context.IsFeatureFlagEnabledAsync(flag))
            {
                return Results.Ok("Operation completed");
            }

            return Results.NotFound();
        });
```

**Controller:**

```csharp
public class AdminController : ControllerBase 
{ 
    [HttpGet("dashboard")] 
    public async Task<IActionResult> GetDashboard() 
    { 
        var evaluator = this.FeatureFlags(); 
        var flag = new AdminDashboardFlag();
        if (await evaluator.IsEnabledAsync(flag))
        {
            return Ok(dashboardData);
        }
    
        return NotFound();
    }
}
```

## Advanced Configuration

### Custom Context Extraction
```csharp
app.UseFeatureFlags(options => 
    { 
        // Custom user ID extraction 
        options.ExtractUserIdFrom(context => 
            context.User.FindFirst("sub")?.Value ?? context.Request.Headers["X-API-Key"].FirstOrDefault() );

        // Custom tenant ID extraction
        options.ExtractTenantIdFrom(context =>
            context.Request.Headers["X-Tenant-ID"].FirstOrDefault()
        );

        // Custom attributes for targeting rules
        options.ExtractAttributes(context =>
        {
            var attributes = new Dictionary<string, object>();
    
            if (context.Request.Headers.TryGetValue("Role", out var role))
                attributes["role"] = role.ToString();
        
            if (context.Request.Headers.TryGetValue("Country", out var country))
                attributes["country"] = country.ToString();
        
            return attributes;
        });
});
```

### Complete Example
```csharp
app.UseFeatureFlags(options => 
    { 
        // Enable maintenance mode 
        options.EnableMaintenanceMode("api-maintenance") 
                .WithMaintenanceResponse(new { message = "Service temporarily unavailable", contact = "support@company.com" });
        
        // Add global feature gates
        options.AddGlobalFlag("beta-features-enabled", 403, new
        {
            error = "Beta features not available for your account"
        });

        // Custom context extraction
        options.ExtractUserIdFrom(context =>
            context.User.FindFirst("sub")?.Value ??
            context.Request.Headers["User-Id"].FirstOrDefault()
        );

        options.ExtractAttributes(context =>
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

## API Reference

### Extension Methods

#### HttpContext Extensions

- `context.FeatureFlags()` - Get the feature flag evaluator
- `context.IsFeatureFlagEnabledAsync(flag)` - Check if a flag is enabled
- `context.GetFeatureFlagVariationAsync<T>(flag, defaultValue)` - Get flag variation value

#### Controller Extensions

- `this.FeatureFlags()` - Get the feature flag evaluator from controller

### Middleware Options Builder

- `EnableMaintenanceMode(flagKey)` - Enable maintenance mode with custom flag key
- `DisableMaintenance()` - Disable maintenance mode
- `WithMaintenanceResponse(response)` - Set custom maintenance response
- `AddGlobalFlag(key, statusCode, response)` - Add a global feature gate
- `ExtractTenantIdFrom(extractor)` - Custom tenant ID extraction
- `ExtractUserIdFrom(extractor)` - Custom user ID extraction
- `ExtractAttributes(extractor)` - Custom attribute extraction

## How It Works

1. **Middleware intercepts requests** and extracts context (tenant, user, attributes)
2. **Global flags are evaluated** - if disabled, request is blocked with configured response
3. **Maintenance mode is checked** - if active, returns 503 Service Unavailable
4. **Evaluator is added to HttpContext.Items** for downstream use
5. **Extensions provide easy access** to evaluate flags in controllers and endpoints

### Dependencies
-	[Propel.FeatureFlags](../../README.md)
-	Microsoft.AspNetCore.Mvc.Core
-	Newtonsoft.Json