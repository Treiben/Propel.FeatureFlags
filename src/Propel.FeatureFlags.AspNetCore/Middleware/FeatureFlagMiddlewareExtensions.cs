using Microsoft.AspNetCore.Builder;

namespace Propel.FeatureFlags.AspNetCore.Middleware;

public static class FeatureFlagMiddlewareExtensions
{
	/// <summary>
	/// Adds feature flag middleware with default options
	/// </summary>
	public static IApplicationBuilder UseFeatureFlags(this IApplicationBuilder app)
	{
		app.UseMiddleware<FeatureFlagMiddleware>(new FeatureFlagMiddlewareOptions());
		return app;
	}

	/// <summary>
	/// Adds feature flag middleware with custom configuration
	/// </summary>
	public static IApplicationBuilder UseFeatureFlags(this IApplicationBuilder app, Action<FeatureFlagMiddlewareOptions> configure)
	{
		var options = new FeatureFlagMiddlewareOptions();
		configure(options);
		app.UseMiddleware<FeatureFlagMiddleware>(options);
		return app;
	}

	/// <summary>
	/// Adds feature flag middleware with custom options instance
	/// </summary>
	public static IApplicationBuilder UseFeatureFlags(this IApplicationBuilder app, FeatureFlagMiddlewareOptions options)
	{
		app.UseMiddleware<FeatureFlagMiddleware>(options);
		return app;
	}
}
