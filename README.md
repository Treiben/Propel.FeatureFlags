# Propel Feature Flags

A comprehensive, centralized feature flag management system designed to bridge the gap between product owners and developers. Built for .NET applications with support for both legacy frameworks and modern .NET Core/8+ applications.

## Overview

Propel Feature Flags provides a robust feature management platform where:
- **Product Owners** control feature releases and configurations through a web interface
- **Developers** focus on implementation using simple flag evaluation APIs
- **Operations Teams** benefit from centralized control and observability

The system automatically creates missing flags in a disabled state, enabling seamless deployment across environments without requiring separate flag provisioning processes.

## Key Benefits

### Centralized Management
- Single source of truth for all feature flags across environments
- Web-based interface for non-technical stakeholders
- Consistent flag behavior across all applications

### Risk Mitigation
- Gradual rollouts with percentage-based targeting
- Tenant-specific feature control for SaaS applications
- Emergency disable capabilities without code deployments

### Developer Experience
- Simple, intuitive APIs
- Automatic flag creation on first reference
- Comprehensive logging and debugging support
- Multiple integration patterns (middleware, attributes, direct calls)

## Core Capabilities

### Flag Types

**Basic Controls**
- **Enabled/Disabled**: Simple on/off switches
- **Scheduled**: Time-based automatic enabling/disabling
- **Time Windows**: Active during specific hours/days with timezone support

**Advanced Targeting**
- **User Targeting**: Enable for specific users or user attributes
- **Percentage Rollouts**: Gradual feature releases with consistent user assignment
- **Multi-Tenant**: Hierarchical evaluation supporting tenant-level controls

### Variations & A/B Testing

Support for complex feature variations beyond simple boolean flags:

```csharp
// Get different pricing configurations
var pricingConfig = await client.GetVariationAsync(
    "pricing-strategy", 
    defaultValue: new { Strategy = "standard", Discount = 0 },
    tenantId: "enterprise-corp",
    userId: "user123");
```

### Targeting Rules

Flexible attribute-based targeting with multiple operators:
- **Equals/NotEquals**: Exact matching
- **Contains/NotContains**: Substring matching  
- **In/NotIn**: List membership
- **GreaterThan/LessThan**: Numeric comparisons

### Multi-Tenancy

Hierarchical evaluation supporting SaaS applications:

1. **Tenant-level controls**: Which tenants can access features
2. **User-level controls**: Which users within allowed tenants get features
3. **Combined targeting**: Rules can reference both tenant and user attributes

## Architecture

### Core Library (.NET Standard 2.0)
- `Propel.FeatureFlags.Core`: Flag definitions and evaluation logic
- `Propel.FeatureFlags.Client`: Evaluation APIs and caching

### Persistence Providers
- `Propel.FeatureFlags.SqlServer`: SQL Server storage
- `Propel.FeatureFlags.PostgresSql`: PostgreSQL storage  
- `Propel.FeatureFlags.Redis`: Redis cache implementation
- `Propel.FeatureFlags.AzureAppConfiguration`: Azure App Config integration

### Web Integration
- `Propel.FeatureFlags.AspNetCore`: Middleware and MVC integration

## Quick Start

### 1. Installation

```bash
# Core library
dotnet add package Propel.FeatureFlags.Core
dotnet add package Propel.FeatureFlags.Client

# Choose your persistence provider
dotnet add package Propel.FeatureFlags.SqlServer
dotnet add package Propel.FeatureFlags.Redis

# ASP.NET Core integration
dotnet add package Propel.FeatureFlags.AspNetCore
```

### 2. Configuration

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.Configure<FlagOptions>(options =>
    {
        options.SqlConnectionString = Configuration.GetConnectionString("Database");
        options.RedisConnectionString = Configuration.GetConnectionString("Redis");
        options.UseCache = true;
        options.DefaultTimeZone = "America/New_York";
    });

    services.AddScoped<IFeatureFlagRepository, SqlServerFeatureFlagRepository>();
    services.AddScoped<IFeatureFlagCache, RedisFeatureFlagCache>();
    services.AddScoped<IFeatureFlagEvaluator, FeatureFlagEvaluator>();
    services.AddScoped<IFeatureFlagClient, FeatureFlagClient>();
}

public void Configure(IApplicationBuilder app)
{
    app.UseFeatureFlags(options =>
    {
        options.EnableMaintenance("maintenance-mode")
               .AddGlobalFlag("api-v2-enabled", 404, new { error = "API v2 not available" })
               .ExtractUserIdFrom(ctx => ctx.User.FindFirst("sub")?.Value)
               .ExtractAttributes(ctx => new Dictionary<string, object>
               {
                   ["region"] = ctx.Request.Headers["X-Region"].FirstOrDefault() ?? "us-east",
                   ["subscription"] = ctx.User.FindFirst("subscription")?.Value ?? "basic"
               });
    });
}
```

### 3. Usage in Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    private readonly IFeatureFlagClient _featureFlags;

    public ProductController(IFeatureFlagClient featureFlags)
    {
        _featureFlags = featureFlags;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var tenantId = User.FindFirst("tenantId")?.Value;
        var userId = User.FindFirst("sub")?.Value;

        // Simple boolean flag
        var useNewCatalog = await _featureFlags.IsEnabledAsync(
            "new-product-catalog", 
            tenantId: tenantId, 
            userId: userId);

        // Complex variation
        var catalogConfig = await _featureFlags.GetVariationAsync(
            "catalog-configuration",
            defaultValue: new CatalogConfig { PageSize = 20, EnableFiltering = false },
            tenantId: tenantId,
            userId: userId);

        if (useNewCatalog)
        {
            return Ok(await GetProductsV2(catalogConfig));
        }
        
        return Ok(await GetProductsV1());
    }

    // Alternative: Using middleware-injected evaluator
    [HttpGet("alternative")]
    public async Task<IActionResult> GetProductsAlternative()
    {
        // Middleware automatically extracts tenant/user context
        var useNewCatalog = await this.FeatureFlags().IsEnabledAsync("new-product-catalog");
        
        return Ok(useNewCatalog ? await GetProductsV2() : await GetProductsV1());
    }
}
```

### 4. Service Layer Usage

```csharp
public class OrderService
{
    private readonly IFeatureFlagClient _featureFlags;

    public async Task<Order> ProcessOrderAsync(string tenantId, string userId, Order order)
    {
        var useAdvancedProcessing = await _featureFlags.IsEnabledAsync(
            "advanced-order-processing", 
            tenantId: tenantId, 
            userId: userId,
            attributes: new Dictionary<string, object>
            {
                ["orderValue"] = order.TotalAmount,
                ["customerTier"] = order.Customer.Tier
            });

        return useAdvancedProcessing 
            ? await ProcessOrderAdvanced(order)
            : await ProcessOrderStandard(order);
    }
}
```

## Flag Configuration Examples

### Basic Feature Toggle
```json
{
  "key": "new-checkout-flow",
  "status": "Enabled",
  "description": "New streamlined checkout process"
}
```

### Percentage Rollout
```json
{
  "key": "experimental-ai-search",
  "status": "Percentage", 
  "percentageEnabled": 25,
  "tenantPercentageEnabled": 50,
  "description": "AI-powered search - 50% of tenants, 25% of users within those tenants"
}
```

### Tenant-Specific Features
```json
{
  "key": "premium-analytics",
  "status": "UserTargeted",
  "enabledTenants": ["enterprise-corp", "big-client-inc"],
  "targetingRules": [
    {
      "attribute": "subscription",
      "operator": "In", 
      "values": ["premium", "enterprise"],
      "variation": "full-analytics"
    },
    {
      "attribute": "subscription",
      "operator": "Equals",
      "values": ["basic"],
      "variation": "basic-analytics"
    }
  ]
}
```

### Time-Based Activation
```json
{
  "key": "holiday-promotions",
  "status": "TimeWindow",
  "windowStartTime": "09:00:00",
  "windowEndTime": "21:00:00", 
  "windowDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
  "timeZone": "America/New_York"
}
```

## Integration Patterns

### Middleware Integration
Automatic extraction of user/tenant context and global feature gates:

```csharp
app.UseFeatureFlags(options =>
{
    options.EnableMaintenance()
           .AddGlobalFlag("api-enabled", 503, new { error = "API temporarily disabled" })
           .ExtractUserIdFrom(ctx => ctx.User.Identity?.Name)
           .ExtractTenantIdFrom(ctx => ctx.User.FindFirst("tenant")?.Value);
});
```

### Attribute-Based Programming
```csharp
[FeatureFlagged("advanced-reporting", fallbackMethod: "BasicReport")]
public async Task<IActionResult> AdvancedReport()
{
    // Advanced reporting implementation
}

public async Task<IActionResult> BasicReport()
{
    // Fallback implementation
}
```

### Direct Client Usage
```csharp
public class PaymentService
{
    public async Task<PaymentResult> ProcessPaymentAsync(Payment payment)
    {
        var provider = await _featureFlags.GetVariationAsync(
            "payment-provider",
            defaultValue: "stripe",
            tenantId: payment.TenantId,
            attributes: new { amount = payment.Amount, currency = payment.Currency });

        return provider switch
        {
            "stripe" => await ProcessWithStripe(payment),
            "paypal" => await ProcessWithPayPal(payment),
            _ => await ProcessWithDefault(payment)
        };
    }
}
```

## Environment Management

Flags are automatically created in a disabled state when first referenced, enabling:

- **Zero-config deployments**: Deploy code without pre-configuring flags
- **Environment consistency**: Same flag keys work across all environments  
- **Gradual enablement**: Product owners enable features post-deployment

## Performance Considerations

- **Caching Layer**: Redis-backed caching with configurable TTL
- **Async Evaluation**: Non-blocking flag evaluation
- **Efficient Hashing**: Consistent user assignment for percentage rollouts
- **Batch Operations**: Support for bulk flag evaluation (coming soon)

## Monitoring & Observability

Comprehensive logging at multiple levels:
- Flag evaluation decisions with context
- Cache hit/miss ratios
- Auto-creation events
- Performance metrics

## Contributing

This is an internal Propel project. For questions or feature requests, contact the development team.

## License

Internal use only - Propel Technologies