# Propel.FeatureFlags for Legacy .NET Framework Applications

Guide for using Propel.FeatureFlags in .NET Framework 4.6.1+ applications without dependency injection.

## Table of Contents

- [Overview](#overview)
- [Compatibility](#compatibility)
- [What's Different](#whats-different)
- [Installation](#installation)
- [Database Repository Implementation](#database-repository-implementation)
- [Container Setup](#container-setup)
- [Application Initialization](#application-initialization)
- [Using Feature Flags](#using-feature-flags)
- [Demo Project Structure](#demo-project-structure)
- [Best Practices](#best-practices)
- [Support](#support)

## Overview

Propel.FeatureFlags core libraries are built on .NET Standard 2.0, making them compatible with .NET Framework 4.6.1 and later. However, legacy applications typically lack modern conveniences like built-in dependency injection and async/await throughout the stack.

This guide shows how to use Propel in legacy .NET Framework applications with minimal changes to your existing codebase.

[⬆ Back to top](#table-of-contents)

## Compatibility

**Supported Frameworks:**
- .NET Framework 4.6.1+
- .NET Framework 4.7.2+ (recommended)
- .NET Framework 4.8+

**Compatible Packages:**
- ✅ `Propel.FeatureFlags` (.NET Standard 2.0)
- ✅ `Propel.FeatureFlags.Infrastructure.Redis` (.NET Standard 2.0)
- ❌ `Propel.FeatureFlags.AspNetCore` (.NET 9.0 only)
- ❌ `Propel.FeatureFlags.Infrastructure.PostgresSql` (.NET 9.0 only)
- ❌ `Propel.FeatureFlags.Infrastructure.SqlServer` (.NET 9.0 only)
- ❌ `Propel.FeatureFlags.Attributes` (.NET 9.0 only)

[⬆ Back to top](#table-of-contents)

## What's Different

Legacy applications require additional boilerplate code because they lack:

| Modern .NET | Legacy .NET Framework | Solution |
|-------------|----------------------|----------|
| Built-in DI container | No DI container | Manual singleton container |
| `IServiceCollection` extensions | N/A | Manual service registration |
| `ConfigureFeatureFlags()` | N/A | Manual initialization in `Application_Start` |
| Database packages included | N/A | Implement `IFeatureFlagRepository` yourself |
| Middleware pipeline | N/A | Manual flag evaluation in controllers |

**You need to implement:**
1. `IFeatureFlagRepository` for your database (SQL Server, PostgreSQL, MySQL, etc.)
2. A singleton container to manage flag services without DI
3. Initialization code in `Global.asax.cs` or startup
4. Manual flag evaluation in controllers or services

[⬆ Back to top](#table-of-contents)

## Installation

Install the core package only:

```bash
# Core library (required)
Install-Package Propel.FeatureFlags

# Optional: Redis caching
Install-Package Propel.FeatureFlags.Infrastructure.Redis

# For SQLite demo (not required for production)
Install-Package Microsoft.Data.Sqlite
```

**Note:** The PostgreSQL and SQL Server packages target .NET 9.0 and are not compatible with .NET Framework. You must implement `IFeatureFlagRepository` yourself for your database.

[⬆ Back to top](#table-of-contents)

## Database Repository Implementation

### 1. Implement IFeatureFlagRepository

You must create a repository implementation for your database. The demo includes a SQLite in-memory implementation as reference.

```csharp
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

public class SqlServerFeatureFlagRepository : IFeatureFlagRepository
{
    private readonly string _connectionString;
    
    public SqlServerFeatureFlagRepository(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public async Task<EvaluationOptions?> GetEvaluationOptionsAsync(
        string key, 
        CancellationToken cancellationToken = default)
    {
        // Query your database for flag configuration
        // Map database columns to EvaluationOptions
        // See demo SqliteFeatureFlagRepository for full implementation
    }
    
    public async Task CreateApplicationFlagAsync(
        string key, 
        EvaluationMode activeMode, 
        string name, 
        string description, 
        CancellationToken cancellationToken = default)
    {
        // Insert flag into your database
        // Handle conflicts (flag already exists)
        // See demo SqliteFeatureFlagRepository for full implementation
    }
}
```

### 2. Database Schema

Your database needs these tables:

**FeatureFlags table:**
```sql
CREATE TABLE FeatureFlags (
    -- Unique identifier
    [Key] NVARCHAR(255) NOT NULL,
    ApplicationName NVARCHAR(255) NOT NULL,
    ApplicationVersion NVARCHAR(50) NOT NULL,
    Scope INT NOT NULL,
    
    -- Descriptive fields
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),
    
    -- Evaluation modes (stored as JSON)
    EvaluationModes NVARCHAR(MAX) NOT NULL,
    
    -- Scheduling
    ScheduledEnableDate DATETIMEOFFSET NULL,
    ScheduledDisableDate DATETIMEOFFSET NULL,
    
    -- Time windows
    WindowStartTime TIME NULL,
    WindowEndTime TIME NULL,
    TimeZone NVARCHAR(100),
    WindowDays NVARCHAR(MAX), -- JSON array
    
    -- User targeting
    EnabledUsers NVARCHAR(MAX), -- JSON array
    DisabledUsers NVARCHAR(MAX), -- JSON array
    UserPercentageEnabled INT NOT NULL DEFAULT 100,
    
    -- Tenant targeting
    EnabledTenants NVARCHAR(MAX), -- JSON array
    DisabledTenants NVARCHAR(MAX), -- JSON array
    TenantPercentageEnabled INT NOT NULL DEFAULT 100,
    
    -- Targeting rules and variations
    TargetingRules NVARCHAR(MAX), -- JSON array
    Variations NVARCHAR(MAX), -- JSON object
    DefaultVariation NVARCHAR(255),
    
    PRIMARY KEY ([Key], ApplicationName, ApplicationVersion)
);
```

**FeatureFlagsMetadata table (optional but recommended):**
```sql
CREATE TABLE FeatureFlagsMetadata (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    FlagKey NVARCHAR(255) NOT NULL,
    ApplicationName NVARCHAR(255) NOT NULL,
    ApplicationVersion NVARCHAR(50) NOT NULL,
    IsPermanent BIT NOT NULL DEFAULT 0,
    ExpirationDate DATETIMEOFFSET NOT NULL,
    Tags NVARCHAR(MAX) -- JSON object
);
```

**FeatureFlagsAudit table (optional but recommended):**
```sql
CREATE TABLE FeatureFlagsAudit (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    FlagKey NVARCHAR(255) NOT NULL,
    ApplicationName NVARCHAR(255),
    ApplicationVersion NVARCHAR(50),
    Action NVARCHAR(100) NOT NULL,
    Actor NVARCHAR(255) NOT NULL,
    Timestamp DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
    Notes NVARCHAR(MAX)
);
```

The demo project includes SQL scripts and entity classes you can adapt for your database.

[⬆ Back to top](#table-of-contents)

## Container Setup

Create a singleton container to manage flag services without a DI framework.

### FeatureFlagContainer.cs

```csharp
using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class FeatureFlagContainer
{
    private static readonly Lazy<FeatureFlagContainer> _instance = 
        new Lazy<FeatureFlagContainer>(() => new FeatureFlagContainer(), true);
    
    public static FeatureFlagContainer Instance => _instance.Value;
    
    private readonly IFeatureFlagRepository _repository;
    private readonly object _factoryLock = new object();
    private readonly object _clientLock = new object();
    private volatile IApplicationFlagClient _client;
    private volatile IFeatureFlagFactory _factory;
    
    private FeatureFlagContainer()
    {
        // Initialize your repository with connection string
        var connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        _repository = new SqlServerFeatureFlagRepository(connectionString);
    }
    
    public IFeatureFlagRepository GetRepository()
    {
        return _repository;
    }
    
    public IFeatureFlagFactory GetOrCreateFlagFactory()
    {
        if (_factory != null)
            return _factory;
        
        lock (_factoryLock)
        {
            if (_factory != null)
                return _factory;
            
            var allFlags = GetAllFlagsFromAssembly();
            _factory = new FeatureFlagFactory(allFlags);
            return _factory;
        }
    }
    
    public IApplicationFlagClient GetOrCreateFlagClient()
    {
        if (_client != null)
            return _client;
        
        lock (_clientLock)
        {
            if (_client != null)
                return _client;
            
            var evaluators = DefaultEvaluators.Create();
            var processor = new ApplicationFlagProcessor(_repository, evaluators);
            _client = new ApplicationFlagClient(processor);
            return _client;
        }
    }
    
    private IEnumerable<IFeatureFlag> GetAllFlagsFromAssembly()
    {
        var flags = new List<IFeatureFlag>();
        var assembly = Assembly.GetExecutingAssembly();
        
        var flagTypes = assembly.GetTypes()
            .Where(t => typeof(IFeatureFlag).IsAssignableFrom(t) 
                && !t.IsInterface 
                && !t.IsAbstract);
        
        foreach (var flagType in flagTypes)
        {
            var instance = (IFeatureFlag)Activator.CreateInstance(flagType);
            flags.Add(instance);
        }
        
        return flags;
    }
}
```

### FeatureFlagService.cs

Create a service wrapper for easy flag evaluation:

```csharp
using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

public class FeatureFlagService
{
    private readonly IApplicationFlagClient _flagClient;
    private readonly IFeatureFlagFactory _flagFactory;
    
    public FeatureFlagService()
    {
        _flagFactory = FeatureFlagContainer.Instance.GetOrCreateFlagFactory();
        _flagClient = FeatureFlagContainer.Instance.GetOrCreateFlagClient();
    }
    
    // Evaluate by flag instance
    public async Task<bool> IsEnabledAsync(
        IFeatureFlag flag,
        string userId = null,
        string tenantId = null,
        Dictionary<string, object> attributes = null)
    {
        var result = await _flagClient.EvaluateAsync(
            flag, 
            tenantId: tenantId,
            userId: userId, 
            attributes: attributes);
        
        return result?.IsEnabled ?? false;
    }
    
    // Evaluate by flag type
    public async Task<bool> IsEnabledAsync<T>(
        string userId = null,
        string tenantId = null,
        Dictionary<string, object> attributes = null)
        where T : IFeatureFlag
    {
        var flag = _flagFactory.GetFlagByType<T>();
        return await IsEnabledAsync(flag, userId, tenantId, attributes);
    }
    
    // Evaluate by flag key
    public async Task<bool> IsEnabledAsync(
        string flagKey,
        string userId = null,
        string tenantId = null,
        Dictionary<string, object> attributes = null)
    {
        var flag = _flagFactory.GetFlagByKey(flagKey);
        if (flag == null)
            throw new Exception($"Flag '{flagKey}' not found");
        
        return await IsEnabledAsync(flag, userId, tenantId, attributes);
    }
    
    // Get variation
    public async Task<string> GetVariationAsync(
        IFeatureFlag flag,
        string defaultVariation,
        string userId = null,
        string tenantId = null,
        Dictionary<string, object> attributes = null)
    {
        return await _flagClient.GetVariationAsync(
            flag,
            defaultValue: defaultVariation,
            tenantId: tenantId,
            userId: userId,
            attributes: attributes);
    }
    
    // Get all flags
    public IEnumerable<IFeatureFlag> GetAllFlags()
    {
        return _flagFactory.GetAllFlags();
    }
}
```

[⬆ Back to top](#table-of-contents)

## Application Initialization

Initialize feature flags in `Global.asax.cs`:

```csharp
using System;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

public class WebApiApplication : HttpApplication
{
    protected void Application_Start()
    {
        AreaRegistration.RegisterAllAreas();
        GlobalConfiguration.Configure(WebApiConfig.Register);
        FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
        RouteConfig.RegisterRoutes(RouteTable.Routes);
        
        // Set application info for flag scoping
        Environment.SetEnvironmentVariable("APP_NAME", "MyLegacyApp");
        Environment.SetEnvironmentVariable("APP_VERSION", "1.0.0");
        
        // Initialize feature flags
        InitializeFeatureFlags();
    }
    
    private void InitializeFeatureFlags()
    {
        try
        {
            // Get singleton container
            var container = FeatureFlagContainer.Instance;
            
            // Initialize database (create tables if needed)
            var dbInitializer = container.GetDatabaseInitializer();
            dbInitializer.InitializeAsync().GetAwaiter().GetResult();
            
            // Get factory and repository
            var factory = container.GetOrCreateFlagFactory();
            var repository = container.GetRepository();
            
            // Auto-deploy all flags to database
            AutoDeployFlags(factory, repository).GetAwaiter().GetResult();
            
            System.Diagnostics.Debug.WriteLine("Feature flags initialized successfully");
        }
        catch (Exception ex)
        {
            // Log error but don't crash the application
            System.Diagnostics.Debug.WriteLine($"Error initializing flags: {ex.Message}");
            throw;
        }
    }
    
    private async System.Threading.Tasks.Task AutoDeployFlags(
        IFeatureFlagFactory factory, 
        IFeatureFlagRepository repository)
    {
        var allFlags = factory.GetAllFlags();
        foreach (var flag in allFlags)
        {
            await repository.CreateApplicationFlagAsync(
                flag.Key,
                flag.OnOffMode,
                flag.Name ?? flag.Key,
                flag.Description ?? "Auto-deployed flag",
                System.Threading.CancellationToken.None);
        }
    }
}
```

[⬆ Back to top](#table-of-contents)

## Using Feature Flags

### 1. Define Flags

Create flag classes just like in modern .NET:

```csharp
using Propel.FeatureFlags.Domain;

namespace MyLegacyApp.FeatureFlags
{
    public class NewProductApiFeatureFlag : FeatureFlagBase
    {
        public NewProductApiFeatureFlag()
            : base(
                key: "new-product-api",
                name: "New Product API",
                description: "Enhanced product API with improved performance",
                onOfMode: EvaluationMode.Off)
        {
        }
    }
}
```

### 2. Use in Controllers

```csharp
using System.Threading.Tasks;
using System.Web.Http;

public class ProductsController : ApiController
{
    private static readonly FeatureFlagService _flagService = new FeatureFlagService();
    
    [HttpGet]
    [Route("api/products")]
    public async Task<IHttpActionResult> GetProducts(string userId = null)
    {
        // Option 1: Evaluate by type (type-safe)
        if (await _flagService.IsEnabledAsync<NewProductApiFeatureFlag>(userId: userId))
        {
            return Ok(GetProductsV2());
        }
        
        return Ok(GetProductsV1());
    }
    
    [HttpGet]
    [Route("api/products/featured")]
    public async Task<IHttpActionResult> GetFeaturedProducts(string userId = null)
    {
        // Option 2: Evaluate by instance
        var flag = new NewProductApiFeatureFlag();
        if (await _flagService.IsEnabledAsync(flag, userId: userId))
        {
            return Ok(GetFeaturedProductsV2());
        }
        
        return Ok(GetFeaturedProductsV1());
    }
    
    private object GetProductsV1() => new { version = "v1", products = new[] { "A", "B" } };
    private object GetProductsV2() => new { version = "v2", products = new[] { "A", "B", "C" } };
    private object GetFeaturedProductsV1() => new { featured = new[] { "Product 1" } };
    private object GetFeaturedProductsV2() => new { featured = new[] { "Premium Product" } };
}
```

### 3. Use with Variations

```csharp
[HttpGet]
[Route("api/recommendations/{userId}")]
public async Task<IHttpActionResult> GetRecommendations(string userId)
{
    var flag = new RecommendationAlgorithmFeatureFlag();
    var algorithm = await _flagService.GetVariationAsync(
        flag,
        defaultVariation: "collaborative-filtering",
        userId: userId);
    
    return algorithm switch
    {
        "machine-learning" => Ok(GetMLRecommendations(userId)),
        "content-based" => Ok(GetContentRecommendations(userId)),
        _ => Ok(GetCollaborativeRecommendations(userId))
    };
}
```

### 4. Use with Attributes

```csharp
[HttpPost]
[Route("api/payments/process")]
public async Task<IHttpActionResult> ProcessPayment([FromBody] PaymentRequest request)
{
    var attributes = new Dictionary<string, object>
    {
        ["amount"] = request.Amount,
        ["currency"] = request.Currency
    };
    
    if (await _flagService.IsEnabledAsync<NewPaymentProcessorFeatureFlag>(
        userId: request.CustomerId,
        attributes: attributes))
    {
        return Ok(ProcessPaymentV2(request));
    }
    
    return Ok(ProcessPaymentV1(request));
}
```

[⬆ Back to top](#table-of-contents)

## Demo Project Structure

The demo project (`/usage-demo/LegacyWebApiDemo`) includes:

```
LegacyWebApiDemo/
├── Controllers/
│   └── DemoController.cs              # Example endpoints with flags
├── FeatureFlags/
│   ├── ApplicationFlags/
│   │   ├── NewProductApiFeatureFlag.cs
│   │   ├── AdminPanelEnabledFeatureFlag.cs
│   │   ├── NewPaymentProcessorFeatureFlag.cs
│   │   └── RecommendationAlgorithmFeatureFlag.cs
│   └── GlobalFlags/                    # (Created via dashboard only)
├── CrossCuttingConcerns/
│   ├── FeatureFlags/
│   │   ├── FeatureFlagContainer.cs    # Singleton container
│   │   ├── FeatureFlagService.cs      # Service wrapper
│   │   └── Sqlite/
│   │       ├── SqliteFeatureFlagRepository.cs  # Example implementation
│   │       ├── SqliteDatabaseInitializer.cs
│   │       └── Helpers/               # Query builders, data readers
└── Global.asax.cs                     # Application startup
```

**Key Files to Adapt:**

1. **SqliteFeatureFlagRepository.cs** - Replace with your database (SQL Server, PostgreSQL, MySQL)
2. **FeatureFlagContainer.cs** - Update connection string and repository initialization
3. **Global.asax.cs** - Review initialization logic for your application

[⬆ Back to top](#table-of-contents)

## Best Practices

### 1. Static Service Instance

Use a static instance of `FeatureFlagService` in controllers to avoid creating new instances on every request:

```csharp
public class MyController : ApiController
{
    private static readonly FeatureFlagService _flagService = new FeatureFlagService();
    
    public async Task<IHttpActionResult> Get()
    {
        if (await _flagService.IsEnabledAsync<MyFeatureFlag>())
        {
            // ...
        }
    }
}
```

### 2. Set Application Info Early

Set `APP_NAME` and `APP_VERSION` environment variables in `Application_Start` before initializing flags:

```csharp
protected void Application_Start()
{
    Environment.SetEnvironmentVariable("APP_NAME", "MyApp");
    Environment.SetEnvironmentVariable("APP_VERSION", "1.0.0");
    
    InitializeFeatureFlags();
}
```

### 3. Handle Initialization Failures Gracefully

Don't crash the application if flag initialization fails:

```csharp
private void InitializeFeatureFlags()
{
    try
    {
        // Initialize flags
    }
    catch (Exception ex)
    {
        // Log error
        System.Diagnostics.Trace.TraceError($"Flag initialization failed: {ex}");
        
        // Continue application startup
        // Flags will auto-create on first evaluation if needed
    }
}
```

### 4. Use In-Memory Cache for Performance

Legacy apps often make synchronous database calls. Use caching to reduce database load:

```csharp
// In FeatureFlagContainer constructor
private FeatureFlagContainer()
{
    _repository = new SqlServerFeatureFlagRepository(connectionString);
    
    // Add in-memory cache
    var cache = new InMemoryFlagCache(); // Implement simple dictionary cache
    _processor = new ApplicationFlagProcessor(_repository, evaluators, cache);
}
```

### 5. Test Database Connection First

Verify database connectivity before initializing flags:

```csharp
private void InitializeFeatureFlags()
{
    try
    {
        // Test connection
        using (var conn = new SqlConnection(connectionString))
        {
            conn.Open();
        }
        
        // Initialize flags
        // ...
    }
    catch (SqlException ex)
    {
        System.Diagnostics.Trace.TraceError($"Database connection failed: {ex}");
        // Decide whether to continue or throw
    }
}
```

### 6. Clean Up Resources

If using persistent connections (like SQLite in-memory), clean up on application shutdown:

```csharp
protected void Application_End()
{
    FeatureFlagContainer.CloseDatabase();
}
```

### 7. Avoid Async/Await in Synchronous Code

If your legacy application doesn't support async throughout, use `.GetAwaiter().GetResult()` carefully:

```csharp
// In synchronous context
public IHttpActionResult GetProducts()
{
    var isEnabled = _flagService.IsEnabledAsync<NewProductApiFeatureFlag>()
        .GetAwaiter()
        .GetResult();
    
    // ...
}
```

**Warning:** This can cause deadlocks in some contexts. Prefer async endpoints when possible.

[⬆ Back to top](#table-of-contents)

## Support

- **Issues**: [GitHub Issues](https://github.com/Treiben/Propel.FeatureFlags/issues)
- **Documentation**: [Main README](../README.md)
- **Demo Project**: [usage-demo/LegacyWebApiDemo](../usage-demo/LegacyWebApiDemo)

[⬆ Back to top](#table-of-contents)
