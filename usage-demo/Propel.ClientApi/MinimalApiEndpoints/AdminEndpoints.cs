using Propel.FeatureFlags.AspNetCore.Extensions;

namespace Propel.ClientApi.MinimalApiEndpoints;

public static class AdminEndpoints
{
	public static void MapAdminEndpoints(this WebApplication app)
	{
		// Global middleware already handled maintenance mode, but you can also check specific flags
		app.MapGet("/admin/sensitive-operation", async (HttpContext context) =>
		{
			if (!await context.IsFeatureFlagEnabledAsync("admin-panel-enabled"))
			{
				return Results.NotFound(); // Feature doesn't exist for this user
			}

			return Results.Ok("Sensitive operation completed");
		});
	}
}
