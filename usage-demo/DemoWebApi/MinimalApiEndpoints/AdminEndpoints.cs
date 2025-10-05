using DemoWebApi.FeatureFlags;
using Propel.FeatureFlags.AspNetCore.Extensions;

namespace DemoWebApi.MinimalApiEndpoints;

public static class AdminEndpoints
{
	public static void MapAdminEndpoints(this WebApplication app)
	{
		// Type-safe feature flag evaluation - RECOMMENDED APPROACH
		// Provides compile-time safety, auto-completion, and better maintainability
		// Uses strongly-typed feature flag definition with default values
		app.MapGet("/admin/sensitive-operation", async (HttpContext context) =>
		{
			var flag = new AdminPanelEnabledFeatureFlag();	// Strongly-typed flag reference
															// Type-safe evaluation ensures the flag exists with proper defaults
															// If flag doesn't exist in database, it will be auto-created with the configured defaults
			if (await context.IsFeatureFlagEnabledAsync(flag))
			{
				return Results.Ok("Sensitive operation completed");
			}

			return Results.NotFound(); // Feature not enabled for this user/context
		});
	}
}
