using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Attributes.Extensions;
using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using System.Reflection;

namespace Propel.FeatureFlags.SqlServer.Extensions;

public static class BuilderExtensions
{
	public static IServiceCollection ConfigureFeatureFlags(this IHostApplicationBuilder builder, Action<PropelConfiguration> configure)
	{
		var config = new PropelConfiguration();
		configure.Invoke(config);

		builder.Services.AddFeatureFlagServices(config);

		var cacheOptions = config.Cache;
		if (cacheOptions.EnableInMemoryCache == true)
		{
			builder.Services.AddInMemoryCache();
		}

		builder.Services.AddFeatureFlagRepository(config.SqlConnection);

		if (config.RegisterFlagsWithContainer)
		{
			builder.Services.RegisterFlagsFromExecutingAssembly();
		}

		var aopOptions = config.Interception;
		if (aopOptions.EnableIntercepter)
		{
			// Register the interceptor service
			builder.Services.AddAttributeInterceptors();
		}

		if (aopOptions.EnableHttpIntercepter)
		{
			// Register the HTTP interceptor service
			builder.Services.AddHttpContextAccessor(); // required for HttpContext-based attributes
			builder.Services.AddHttpAttributeInterceptors();
		}

		if (config.EnableFlagFactory)
			builder.Services.TryAddSingleton<IFeatureFlagFactory, FeatureFlagFactory>();

		return builder.Services;
	}

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

	public static async Task AutoDeployFlags(this IHost host)
	{
		var repository = host.Services.GetRequiredService<IFeatureFlagRepository>();

		if (repository is null)
			throw new InvalidOperationException("Feature flag repository is not available. " +
				"Make sure you added necessary flag services by calling ConfigureFeatureFlags() method from Propel.CoreExtensions.PostgreSql namespace.");

		// Get all registered feature flags from the factory
		var factory = host.Services.GetService<IFeatureFlagFactory>();
		if (factory is null)
		{
			await DeployFromAssemblyAsync(repository);
			return;
		}
		var allFlags = factory.GetAllFlags();
		foreach (var flag in allFlags)
		{
			await flag.DeployAsync(repository);
		}
	}

	private static async Task DeployFromAssemblyAsync(IFeatureFlagRepository repository)
	{
		var currentAssembly = Assembly.GetExecutingAssembly();
		var allFlags = currentAssembly
			.GetTypes()
			.Where(t => typeof(IFeatureFlag).IsAssignableFrom(t)
					&& !t.IsInterface
					&& !t.IsAbstract);
		foreach (var flag in allFlags)
		{
			var instance = (IFeatureFlag)Activator.CreateInstance(flag)!;
			await instance.DeployAsync(repository);
		}
	}
}
