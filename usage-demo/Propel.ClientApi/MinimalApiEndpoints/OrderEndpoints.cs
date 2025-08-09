using Propel.FeatureFlags.AspNetCore.Extensions;

namespace Propel.ClientApi.MinimalApiEndpoints;

public record CreateOrderRequest(string ProductId, int Quantity, double TotalPrice);

public static class OrderEndpoints
{
	public static WebApplication MapOrderEndpoints(this WebApplication app)
	{
		//=== PERCENTAGE ROLLOUT FEATURE FLAG DEMO (Status: Percentage) ===
		//
		// ✅ CORRECT USAGE - Percentage Rollout with Variations:
		// • A/B testing different technical implementations
		// • Gradual rollout of new processing algorithms
		// • Performance testing with controlled user groups
		// • Canary releases of new technical features
		// • Risk mitigation during technical migrations
		// • Testing new API integrations
		//
		// ❌ INCORRECT USAGE:
		// • User subscription tiers or pricing plans
		// • Access control
		// • Geographic restrictions or compliance rules
		// • Business logic
		// • User permissions or role-based access
		//
		// This flag controls WHICH ORDER PROCESSING IMPLEMENTATION runs
		// behind the scenes. All users can place orders regardless of the
		// variation - we're testing different technical approaches to
		// process the same business operation.
		//
		// THE PERCENTAGE ROLLOUT:
		// - 33% of users get the new technical implementation (v2)
		// - Some users might get experimental implementation (v3)
		// - Remaining users get the proven legacy implementation (v1)
		//
		// This allows us to safely test new order processing logic while
		// ensuring most users get the stable, proven implementation.
		// The business outcome (order placed successfully) is the same
		// regardless of which technical variation processes it.
		//
		app.MapPost("/orders", async(CreateOrderRequest request, HttpContext context) =>
		{
			// Get variation for technical A/B testing - this controls HOW we process, not WHAT we process
			var checkoutVersion = await context.GetFeatureFlagVariationAsync("checkout-version", "v1");
			return checkoutVersion switch
			{
				"v2" => Results.Ok(ProcessOrderV2(request, context)), // New technical implementation
				"v3" => Results.Ok(ProcessOrderV3(request, context)), // Experimental technical approach
				_ => Results.Ok(ProcessOrderV1(request, context))     // Legacy proven implementation
			};
		});
		return app;
	}

	// All methods accomplish the same BUSINESS GOAL (process order)
	// Only the TECHNICAL IMPLEMENTATION differs
	private static object ProcessOrderV1(CreateOrderRequest request, HttpContext context) =>
		new { 
			version = "v1", 
			orderId = Guid.NewGuid(), 
			message = "Order processed with legacy flow",
			implementation = "Proven, stable order processing pipeline"
		};

	private static object ProcessOrderV2(CreateOrderRequest request, HttpContext context) =>
		new { 
			version = "v2", 
			orderId = Guid.NewGuid(), 
			message = "Order processed with new flow",
			implementation = "Enhanced order processing with optimizations"
		};

	private static object ProcessOrderV3(CreateOrderRequest request, HttpContext context) =>
		new { 
			version = "v3", 
			orderId = Guid.NewGuid(), 
			message = "Order processed with experimental flow",
			implementation = "Cutting-edge processing algorithms (experimental)"
		};
}
