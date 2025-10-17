## Propel.FeatureFlags
[![Build and Test](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Propel.FeatureFlags.svg)](https://www.nuget.org/packages/Propel.FeatureFlags/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Propel.FeatureFlags.svg?style=flat-square)](https://www.nuget.org/packages/Propel.FeatureFlags/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-standard-versions)

A flexible, type-safe feature flag library for .NET applications that supports progressive rollouts, A/B testing, and dynamic feature management.

For detailed documentation and examples, visit the repository [readme](../../README.md).

### Features
-	Type-Safe Feature Flags - Define feature flags as strongly-typed classes with compile-time safety
-	Multiple Evaluation Modes - Support for simple on/off, scheduled releases, time windows, and targeted rollouts
-	Progressive Rollouts - Gradually roll out features to users or tenants based on percentages or targeting rules
-	A/B Testing & Variations - Test different implementations with variation support
-	Auto-Registration - No deployment step is needed to add new flags: flags are automatically created in the database on first use
-	Caching Support - Built-in caching for improved performance
-	Multi-Tenant Ready - Evaluate flags based on tenant, user, or custom attributes
  - 
### Installation
```bash
dotnet add package Propel.FeatureFlags
```

### Quick Start
For complete examples including usage in .dotnet legacy frameworks, visit the [usage-demo](../../usage-demo/) projects.
#### 1. Define a Feature Flag
```csharp
public class NewProductApiFeatureFlag : FeatureFlagBase
{
    public NewProductApiFeatureFlag()
        : base(
            key: "new-product-api",
            name: "New Product API",
            description: "Controls Product API versioning (v1 vs v2)",
            onOfMode: EvaluationMode.Off)
    {
    }
}
```

#### 2. Evaluate the Flag (Application-Scoped)
Simple setup inside of your application code.
```csharp

//set up optional attributes for targeting rules and variations
var attributes = new Dictionary<string, object>
{
	["amount"] = request.Amount,
	["currency"] = request.Currency,
	["country"] = request.BillingAddress.Country,
	["paymentMethod"] = request.PaymentMethod,
	["riskScore"] = await CalculateRiskScore(request)
};

// access the flag client via DI if registered in your service collection
IApplicationFlagClient flagClient = serviceProvider.GetRequiredService<IApplicationFlagClient>();
```
Use [IApplicationFlagClient](./Clients/IApplicationFlagClient.cs) for application-specific flags that auto-register:
```csharp
// Simple on/off check
var flag = new NewProductApiFeatureFlag();

var isEnabled = await flagClient.IsEnabledAsync(
    flag, 
    tenantId: "tenant-123", 
    userId: "user-456",
    attributes: attributes //optional targeting context
);

if (isEnabled)
{
    return GetProductsV2(); // New implementation
}
return GetProductsV1(); // Legacy implementation
```

#### 3. Use Variations for A/B Testing
```csharp
// Get specific variation value
var algorithmType = await _flagClient.GetVariationAsync(
    flag: new RecommendationAlgorithmFeatureFlag(),
    defaultValue: "collaborative-filtering",
    userId: userId,
    tenantId: "tenant-123",
    attributes: attributes // optional targeting context
);

return algorithmType switch
{
    "machine-learning" => GetMLRecommendations(userId),
    "content-based" => GetContentBasedRecommendations(userId),
    _ => GetCollaborativeRecommendations(userId)
};
```
#### 4. Global Flags (Cross-Application)
Use [IGlobalFlagClient](./Clients/IGlobalFlagClient.cs) for flags shared across multiple applications:
```csharp
var isEnabled = await _globalFlagClient.IsEnabledAsync(
    flagKey: "maintenance-mode",
    tenantId: "tenant-123",
    ... // other parameters as needed
);
```

#### 5. .NET Dependency Injection Setup
```csharp
public static IServiceCollection AddFeatureFlagServices(this IServiceCollection services, PropelConfiguration propelConfiguration)
{
	services.AddSingleton(propelConfiguration);

	services.AddSingleton<IApplicationFlagProcessor, ApplicationFlagProcessor>();
	services.AddSingleton<IApplicationFlagClient, ApplicationFlagClient>();

	services.AddSingleton<IGlobalFlagProcessor, GlobalFlagProcessor>();
	services.AddSingleton<IGlobalFlagClient, GlobalFlagClient>();

	// Register evaluation manager with all handlers
	services.RegisterEvaluators();

	return services;
}
```
Register evaluators extension method:
```csharp
internal static IServiceCollection RegisterEvaluators(this IServiceCollection services)
{
	// Register evaluation manager with all handlers (can be customized for your use cases)
	services.AddSingleton<IEvaluatorsSet>(_ => new EvaluatorsSet(
		new HashSet<IEvaluator>(
			[   new ActivationScheduleEvaluator(),
				new OperationalWindowEvaluator(),
				new TargetingRulesEvaluator(),
				new TenantRolloutEvaluator(),
				new UserRolloutEvaluator(),
			])));
	return services;
}
```
Enable auto-registration from your assembly containing feature flag definitions:
```csharp
internal static IServiceCollection RegisterFlagsFromExecutingAssembly(this IServiceCollection services)
{
	var currentAssembly = Assembly.GetEntryAssembly();

	var allFlags = currentAssembly
		.GetTypes()
		.Where(t => typeof(IFeatureFlag).IsAssignableFrom(t)
				&& !t.IsInterface
				&& !t.IsAbstract);

	foreach (var flag in allFlags)
	{
		var instance = (IFeatureFlag)Activator.CreateInstance(flag)!;
		services.AddSingleton(instance);
	}

	return services;
}
```
Configure local caching (optional) with [LocalCacheConfiguration](./Infrastructure/Cache/LocalCacheConfiguration.cs):
```csharp
internal static IServiceCollection AddLocalCache(this IServiceCollection services, LocalCacheConfiguration cacheConfiguration)
{
	// Add memory cache for local caching layer
	services.AddMemoryCache(memOptions =>
	{
		memOptions.SizeLimit = cacheConfiguration.CacheSizeLimit;
		memOptions.CompactionPercentage = 0.25; // Compact 25% when limit reached
	});
	services.TryAddSingleton(cacheConfiguration);
	services.TryAddSingleton<IFeatureFlagCache, LocalFeatureFlagCache>();
	return services;
}
```

### Evaluation Modes
-	**Off/On** - Simple disabled/enabled state
-	**Scheduled** - Activate at a specific date/time
-	**TimeWindow** - Active only during specified time ranges
-	**UserTargeted** - Target specific users or rollout percentages
-	**TenantTargeted** - Target specific tenants or rollout percentages
-	**TargetingRules** - Complex rules based on custom attributes
  
### ASP.NET Core Integration
```csharp
app.MapGet("/products", async (HttpContext context, IFeatureFlagFactory flags) =>
{
    var flag = flags.GetFlagByType<NewProductApiFeatureFlag>();
    if (await context.IsFeatureFlagEnabledAsync(flag))
    {
        return Results.Ok(GetProductsV2());
    }
    return Results.Ok(GetProductsV1());
});
```
### Best Practices
✅ Use feature flags for:
-	Technical implementation changes (v1 → v2 APIs)
-	Gradual rollouts and canary deployments
-	A/B testing different technical approaches
-	Time-sensitive feature releases
-	Beta features with controlled access
  
❌ Don't use feature flags for:
-	Business logic or user permissions
-	Subscription tiers or pricing rules
-	Data access control
-	Permanent business rules
  
### Architecture
|	Layer	|	Description	| 
|	------	|	-----------	| 
|	Flag Client Layer		|	IApplicationFlagClient / IGlobalFlagClient	|
|	Flag Processor Layer	|	Evaluation logic & caching	|
|	Evaluators Layer		|	Scheduled, TimeWindow, Targeting, Rollout	|
|	Repository Layer		|	IFeatureFlagRepository (your implementation)	|


---
For detailed documentation and examples, visit the [GitHub repository](https://github.com/Treiben/propel-feature-flags-csharp).