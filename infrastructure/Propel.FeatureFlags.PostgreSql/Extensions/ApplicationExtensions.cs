using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Propel.FeatureFlags.PostgreSql.Extensions;

public static class ApplicationExtensions
{
	/// <summary>
	/// Initializes the feature flags database by ensuring that the necessary database and tables exist.
	/// </summary>
	/// <remarks>This method is typically called during application startup to prepare the feature flags database.
	/// If the database initialization fails, the application will log the error and continue running, except in production
	/// environments where the application will terminate to prevent inconsistent behavior.</remarks>
	/// <param name="host">The <see cref="IHost"/> instance used to access application services.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	public static async Task InitializeFeatureFlagsDatabase(this IHost host)
	{
		var environment = host.Services.GetService<IHostEnvironment>();
		var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(environment!.ApplicationName);
		try
		{
			logger.LogInformation("Starting feature flags database initialization...");

			// This will create the database and tables if they don't exist
			await host.Services.EnsureFeatureFlagDatabase();

			logger.LogInformation("Feature flags database initialization completed successfully");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to initialize feature flags database. Application will continue but feature flags may not work properly.");

			if (environment != null && environment.IsProduction())
			{
				// In production, you might want to exit if database setup fails
				logger.LogCritical("Exiting application due to database initialization failure in production environment");
				throw; // Re-throw to stop application startup
			}
			// In development/staging, continue running so developers can troubleshoot
		}
	}
}
