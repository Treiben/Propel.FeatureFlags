using Propel.FeatureFlags.AspNetCore.Extensions;

namespace Propel.ClientApi.MinimalApiEndpoints;

public static class AdminEndpoints
{
	public static void MapAdminEndpoints(this WebApplication app)
	{
		// Legacy string-based feature flag check (v1)
		// Uses string key which is more error-prone, lacks compile-time safety, and is harder to maintain if used in multiple locations in code
		app.MapGet("/v1/admin/sensitive-operation", async (HttpContext context) =>
		{
			if (!await context.IsFeatureFlagEnabledAsync("admin-panel-enabled"))
			{
				return Results.NotFound(); // Feature doesn't exist for this user
			}

			return Results.Ok("Sensitive operation completed");
		});

		// Type-safe feature flag evaluation (v2) - RECOMMENDED APPROACH
		// Provides compile-time safety, auto-completion, and better maintainability
		// Uses strongly-typed feature flag definition with default values
		app.MapGet("/v2/admin/sensitive-operation", async (HttpContext context) =>
		{
			// Type-safe evaluation ensures the flag exists with proper defaults
			// If flag doesn't exist in database, it will be auto-created with the configured defaults
			if (await context.IsFeatureFlagEnabledAsync(ApplicationFeatureFlags.AdminPanelEnabledFeatureFlag))
			{
				return Results.Ok("Sensitive operation completed");
			}

			return Results.NotFound(); // Feature not enabled for this user/context
		});
	}
}
