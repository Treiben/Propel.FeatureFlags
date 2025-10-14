using DemoWebApi.FeatureFlags;
using Propel.FeatureFlags.AspNetCore.Extensions;
using Propel.FeatureFlags.Clients;

namespace DemoWebApi.MinimalApiEndpoints;

/// <summary>
///=== Complex feature flag demo: user-specific recommendation algorithms ===
///
/// ⚠️  IMPORTANT: When to use VARIATIONS and TARGETING feature flags ⚠️
///
/// This type of feature flag (with variations and user targeting) should be used for:
/// ✅ TECHNICAL ROLLOUTS - Different algorithm implementations
/// ✅ PERFORMANCE TESTING - Comparing algorithm performance
/// ✅ GRADUAL MIGRATION - Moving from old to new technical implementations
/// ✅ A/B TESTING - Testing different technical approaches
/// ✅ CANARY RELEASES - Rolling out new code to specific user groups
///
/// ❌ DO NOT use feature flags for:
/// ❌ BUSINESS LOGIC - User subscription levels, pricing tiers, permissions
/// ❌ DATA ACCESS - What data users can see based on their plan
/// ❌ OPERATIONAL WORKFLOWS - Business processes, approval flows
/// ❌ USER ENTITLEMENTS - Features users have paid for or earned
///
/// This flag controls WHICH ALGORITHM runs behind the scenes - it's a technical
/// implementation detail. All users get recommendations, but we're testing different
/// technical approaches to generate them. The business logic (showing recommendations)
/// remains the same regardless of the flag state.
///
/// TARGETING RULES EXAMPLE:
/// - Premium users get ML algorithm (to test performance on engaged users)
/// - Users in specific regions get content-based (to test regional relevance)
/// - Default users get collaborative filtering (proven, stable algorithm)
///
/// This allows us to safely test new technical implementations while maintaining
/// the same business functionality for all users.
/// </summary>

public static class RecommendationsEndpoints
{
	public static void MapRecommendationsEndpoints(this WebApplication app)
	{
		// Type-safe feature flag evaluation
		// Provides compile-time safety, auto-completion, and better maintainability
		// Uses strongly-typed feature flag definition with default values
		app.MapGet("/recommendations/{userId}", async (string userId, IApplicationFlagClient featureFlags) =>
		{
			// Type-safe evaluation ensures the flag exists with proper defaults
			// If flag doesn't exist in database, it will be auto-created with the configured defaults
			var featureFlag = new RecommendationAlgorithmFeatureFlag();
			var algorithmType = await featureFlags.GetVariationAsync(
				flag: featureFlag,
				defaultValue: "collaborative-filtering", // default
				userId: userId
			);

			return algorithmType switch
			{
				"machine-learning" => Results.Ok(GetMLRecommendations(userId)),
				"content-based" => Results.Ok(GetContentBasedRecommendations(userId)),
				_ => Results.Ok(GetCollaborativeRecommendations(userId))
			};
		});
	}

	// All methods return recommendations - the BUSINESS LOGIC is the same
	// Only the TECHNICAL IMPLEMENTATION differs
	private static object GetMLRecommendations(string userId) =>
		new { 
			algorithm = "ML", 
			recommendations = new[] { "AI Product 1", "AI Product 2" },
			note = "Generated using machine learning algorithms"
		};

	private static object GetContentBasedRecommendations(string userId) =>
		new { 
			algorithm = "Content", 
			recommendations = new[] { "Similar Product 1", "Similar Product 2" },
			note = "Generated using content similarity analysis"
		};

	private static object GetCollaborativeRecommendations(string userId) =>
		new { 
			algorithm = "Collaborative", 
			recommendations = new[] { "Popular Product 1", "Popular Product 2" },
			note = "Generated using collaborative filtering"
		};
}
