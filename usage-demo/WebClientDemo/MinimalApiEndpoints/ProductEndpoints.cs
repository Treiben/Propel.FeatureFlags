using ApiFlagUsageDemo.FeatureFlags;
using Propel.FeatureFlags.AspNetCore.Extensions;
using Propel.FeatureFlags.Clients;

namespace ApiFlagUsageDemo.MinimalApiEndpoints;

public static class ProductEndpoints
{
	public static void MapProductEndpoints(this WebApplication app)
	{
		///<summary>
		///=== SIMPLE FEATURE FLAG DEMO (Status: Disabled/Enabled) ===
		///
		/// ✅ CORRECT USAGE - Simple On/Off Feature Flags:
		/// • New API implementations or endpoints
		/// • UI component changes or redesigns
		/// • Performance optimizations, bug fixes, etc. - any technical changes can can be quickly rolled back without redeploying
		///
		/// This example shows switching between API implementations
		/// a technical use case where we're testing a new product API version.
		///</summary>

		// Type-safe feature flag evaluation
		// Provides compile-time safety, auto-completion, and better maintainability
		// Uses strongly-typed feature flag definition with default values
		app.MapGet("/products", async (
			HttpContext context,
			IFeatureFlagFactory flags) =>
		{
			var flag = flags.GetFlagByType<NewProductApiFeatureFlag>();
			// Type-safe evaluation ensures the flag exists with proper defaults
			// If flag doesn't exist in database, it will be auto-created with the configured defaults
			if (await context.IsFeatureFlagEnabledAsync(flag))
			{
				return Results.Ok(GetProductsV2()); // New technical implementation
			}

			return Results.Ok(GetProductsV1()); // Legacy technical implementation
		});

		///<summary>
		///=== SCHEDULED FEATURE FLAG DEMO (Status: Scheduled) ===
		///
		/// ✅ CORRECT USAGE - Scheduled Feature Flags:
		/// • Coordinated technical releases across teams
		/// • New feature launches that need precise timing
		/// • System migrations with specific go-live dates
		/// • Marketing campaign technical components
		/// • Maintenance window activations
		///
		/// ❌ INCORRECT USAGE:
		/// • Actual business rules
		/// • Business event scheduling
		///
		/// This example demonstrates launching a new technical implementation
		/// (enhanced featured products display) at a specific time, which is
		/// appropriate for coordinating technical releases.
		/// </summary>

		app.MapGet("/products/featured", async (
			HttpContext context,
			IFeatureFlagFactory flags) =>
		{
			var flag = flags.GetFlagByType<FeaturedProductsLaunchFeatureFlag>();
			if (await context.IsFeatureFlagEnabledAsync(flag))
			{
				return Results.Ok(GetFeaturedProductsV2()); // New enhanced display
			}

			return Results.Ok(GetFeaturedProductsV1()); // Standard display
		});

		/// <summary>
		///=== TIME WINDOW FEATURE FLAG DEMO (Status: TimeWindow) ===
		/// ✅ CORRECT USAGE - Time Window Feature Flags:
		/// • Features requiring live support availability
		/// • Resource-intensive operations during off-peak hours
		/// • External service integrations with limited hours
		/// • Beta features with monitoring during business hours
		/// • Complex UI that needs support team availability
		///
		/// ❌ INCORRECT USAGE:
		/// • Business operating hours
		/// • Store hours or service availability
		/// • User timezone-based content
		/// • Regional or any other business logic
		///
		/// This example shows enabling enhanced UI features only when support
		/// staff is available to help users with the more complex interface -
		/// a valid technical consideration.
		/// </summary>

		app.MapGet("/products/catalog", async (
			HttpContext context,
			IFeatureFlagFactory flags) =>
		{
			var flag = flags.GetFlagByType<EnhancedCatalogUiFeatureFlag>();
			if (await context.IsFeatureFlagEnabledAsync(flag))
			{
				return Results.Ok(GetEnhancedCatalogView()); // Enhanced UI with support-dependent features
			}

			return Results.Ok(GetStandardCatalogView()); // Standard UI that works without support
		});
	}

	// TECHNICAL IMPLEMENTATIONS - Same business logic, different technical approaches

	private static object GetProductsV1() =>
		new { version = "v1", products = new[] { "Product A", "Product B" } };

	private static object GetProductsV2() =>
		new { version = "v2", products = new[] { "Product A", "Product B", "Product C" } };

	private static object GetFeaturedProductsV1() =>
		new { 
			version = "legacy", 
			featured = new[] { "Standard Product", "Basic Product" },
			promotion = "Regular pricing"
		};

	private static object GetFeaturedProductsV2() =>
		new { 
			version = "launch", 
			featured = new[] { "Premium Product X", "Exclusive Product Y", "Limited Edition Z" },
			promotion = "Launch special: 25% off all featured items!",
			launchDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
		};

	private static object GetStandardCatalogView() =>
		new { 
			catalogVersion = "standard",
			products = new[] { 
				new { id = 1, name = "Product Alpha", price = 29.99 },
				new { id = 2, name = "Product Beta", price = 49.99 }
			},
			features = new[] { "basic-search", "simple-filters" }
		};

	private static object GetEnhancedCatalogView() =>
		new { 
			catalogVersion = "enhanced",
			products = new[] { 
				new { id = 1, name = "Product Alpha", price = 29.99, rating = 4.5, reviews = 123, inStock = true },
				new { id = 2, name = "Product Beta", price = 49.99, rating = 4.8, reviews = 89, inStock = true }
			},
			features = new[] { "advanced-search", "smart-filters", "recommendations", "live-chat", "detailed-analytics" },
			supportAvailable = true,
			message = "Enhanced catalog with live support available during business hours"
		};
}
