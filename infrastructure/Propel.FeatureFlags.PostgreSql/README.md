## Propel.FeatureFlags.PostgreSql
[![Build and Test](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Propel.FeatureFlags.PostgreSql.svg)](https://www.nuget.org/packages/Propel.FeatureFlags.PostgreSql/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Propel.FeatureFlags.PostgreSql.svg?style=flat-square)](https://www.nuget.org/packages/Propel.FeatureFlags.PostgreSql/)
![.NET Core](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)

PostgreSQL repository implementation for Propel.FeatureFlags, providing persistent storage for feature flag configurations and evaluations.

### Features
-	PostgreSQL Storage - Feature flag storage using PostgreSQL
-	Connection Pooling - Connection management with configurable pool settings
-	Auto-Initialization - Automatic database and table creation on startup
-	Resilience Configuration - Built-in timeout and retry settings for production workloads
-	Schema Management - Handles database schema creation and migrations automatically
  
### Installation
```bash
dotnet add package Propel.FeatureFlags.PostgreSql
```

### Quick Start

#### Basic Configuration
```csharp
using Propel.FeatureFlags.PostgreSql.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add PostgreSQL feature flag repository
builder.Services
    .ConfigureFeatureFlags(config =>
    {
        config.RegisterFlagsWithContainer = true;
        config.EnableFlagFactory = true;
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
#### Console Application
```csharp
using Propel.FeatureFlags.PostgreSql.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .ConfigureFeatureFlags(config =>
    {
        config.RegisterFlagsWithContainer = true;
        config.EnableFlagFactory = true;
    })
    .AddPostgreSqlFeatureFlags(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

// Initialize database in development
if (app.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
{
    await app.InitializeFeatureFlagsDatabase();
}

await app.AutoDeployFlags();

await app.RunAsync();
```
### Configuration

****Connection String****
The extension automatically configures the PostgreSQL connection with optimized settings:
```csharp
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost,5432;Database=feature_flags;Search Path=example;Username=postgres-user;Password=postgres-password"
  }
}
```

### Automatic Connection Optimization

The AddPostgreSqlFeatureFlags() method automatically applies the following settings:

| Setting | Value | Description | 
|---------|-------|-------------| 
| CommandTimeout | 30 seconds | Maximum time for command execution | 
| Timeout | 15 seconds | Connection timeout | 
| MaxPoolSize | 100 | Maximum connections in pool | 
| MinPoolSize | 5 | Minimum connections in pool | 
| Pooling | true | Enable connection pooling | 
| ConnectionIdleLifetime | 300 seconds | Idle connection lifetime | 
| ConnectionPruningInterval | 10 seconds | Pool cleanup interval | 
| ApplicationName | PropelFeatureFlags | Identifier in PostgreSQL logs |

### Extension Methods

`AddPostgreSqlFeatureFlags(string connectionString)` registers PostgreSQL as the feature flag repository with optimized connection settings.

```csharp
builder.Services
    .ConfigureFeatureFlags(config => { ... })
    .AddPostgreSqlFeatureFlags(connectionString);
```

`InitializeFeatureFlagsDatabase()` creates the database schema if it doesn't exist. Recommended for development environments only.
```csharp
if (app.Environment.IsDevelopment())
{
    await app.InitializeFeatureFlagsDatabase();
}
```

#### Important Notes:
-	In production, this method will throw an exception and stop the application if initialization fails
-	In development/staging, the application continues running to allow troubleshooting
-	Only call this method during application startup, not on every request

### Database Schema

The extension automatically creates the following database objects:
-	Tables - Feature flags, variations, targeting rules, rollout configurations
-	Indexes - Optimized for common query patterns
-	Constraints - Ensures data integrity
  
Schema creation happens automatically when calling `InitializeFeatureFlagsDatabase()`.

#### Production Deployment

For production environments, it's recommended to deploy the database schema from CI/CD pipelines instead of using `InitializeFeatureFlagsDatabase()`. 
You can use the [Propel CLI](https://github.com/Treiben/propel-cli) utility to automate schema deployment as part of your deployment pipeline. 
This approach provides better control over database changes, enables schema versioning, and follows infrastructure-as-code best practices.

Example CI/CD pipeline command
`propel-cli migrate --connection-string "Host=prod_server;Database=feature_flags;..."`


### Usage Example

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .ConfigureFeatureFlags(config =>
            {
                config.RegisterFlagsWithContainer = true;
                config.EnableFlagFactory = true;
            })
            .AddPostgreSqlFeatureFlags(Configuration.GetConnectionString("DefaultConnection")!);
    }

    public async Task Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            // Create database schema
            await app.ApplicationServices
                .GetRequiredService<IHost>()
                .InitializeFeatureFlagsDatabase();
        }

        // Deploy flags to database
        await app.ApplicationServices
            .GetRequiredService<IHost>()
            .AutoDeployFlags();
    }
}
```

### Related Packages
-	[Propel.FeatureFlags](../../src/Propel.FeatureFlags/) - Core feature flag library    
-	[Propel.FeatureFlags.DependencyInjection.Extensions](../Propel.FeatureFlags.DependencyInjection.Extension/) - Container extensions
-	[Propel.FeatureFlags.Redis](../Propel.FeatureFlags.Redis/) - Redis caching provider
-	[Propel.FeatureFlags.AspNetCore](../../src/Propel.FeatureFlags.AspNetCore/) - ASP.NET Core middleware
-	[Propel.FeatureFlags.Attributes](../../src/Propel.FeatureFlags.Attributes/) - Attribute-based feature flagging