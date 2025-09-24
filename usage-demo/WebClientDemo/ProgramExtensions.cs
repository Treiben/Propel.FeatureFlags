using ApiFlagUsageDemo.FeatureFlags;

using Propel.FeatureFlags.AspNetCore.Middleware;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Extensions;

using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;
using Propel.FeatureFlags.Infrastructure.Redis.Extensions;
using System.Reflection;

namespace WebClientDemo;

public static class BuilderExtensions
{
	public static void ConfigureFeatureFlags(this WebApplicationBuilder builder, Action<PropelOptions> configure)
	{
		var options = new PropelOptions();
		configure.Invoke(options);

		builder.Services.AddFeatureFlagServices(options);

		var cacheOptions = options.Cache;
		if (cacheOptions.EnableDistributedCache == true)
		{
			builder.Services.AddFeatureFlagRedisCache(cacheOptions.Connection);
		}
		else if (cacheOptions.EnableInMemoryCache == true)
		{
			builder.Services.AddFeatureFlagDefaultCache();
		}

		var dbOptions = options.Database;
		builder.Services.AddFeatureFlagPersistence(dbOptions.ConnectionString);

		if (options.RegisterFlagsWithContainer)
		{
			builder.Services.RegisterAllFlags();
		}

		var aopOptions = options.AttributeIntercepting;
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
	}
}

public static class AppExtensions
{
	public static WebApplication AddFeatureFlagMiddleware(this WebApplication app, string scenario = "basic")
	{
		return scenario switch
		{
			// Simple middleware configuration example:
			// Feature flag middleware with maintenance mode enabled - global kill mode that'll shut down the API
			// This will automatically check the "api-maintenance" flag and return a 503 Service Unavailable response if enabled
			"maintenance" => (WebApplication)app.UseFeatureFlags(options =>
							{
								options.EnableMaintenanceMode = true;
								options.MaintenanceFlagKey = "api-maintenance";
								options.MaintenanceResponse = new
								{
									message = "API is temporarily down for maintenance",
									estimatedDuration = "30 minutes",
									contact = "support@company.com"
								};

							}),
			// Slightly more complex middleware configuration example:
			// Feature flag middleware with global flags
			// This allows you to define global feature gates that apply to all users
			"global" => (WebApplication)app.UseFeatureFlags(options =>
							{
								// Add global feature gates
								options.GlobalFlags.Add(new GlobalFlag
								{
									Key = "api-v2-enabled",
									StatusCode = 410, // Gone
									Response = new { error = "API v2 is no longer available, please upgrade to v3" }
								});
								options.GlobalFlags.Add(new GlobalFlag
								{
									Key = "beta-features-enabled",
									StatusCode = 403, // Forbidden
									Response = new { error = "Beta features not available for your account" }
								});
							}),
			// For more advanced feature flags, such as A/B or user percentage,
			// example of feature flag middleware with custom user ID extraction and attribute extractors
			"user-extraction" => (WebApplication)app.UseFeatureFlags(options =>
							{
								// Custom user ID extraction
								options.UserIdExtractor = context =>
								{
									// Try JWT token first
									var jwtUserId = context.User.FindFirst("sub")?.Value;
									if (!string.IsNullOrEmpty(jwtUserId))
										return jwtUserId;
									// Try API key header
									var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
									if (!string.IsNullOrEmpty(apiKey))
										return $"api:{apiKey}";
									// Try session ID
									return context.Request.Headers["X-Session-ID"].FirstOrDefault();
								};
							}),
			// Example of feature flag middleware configuration for SaaS with custom attribute extractors
			// for example, extracting tenant information, API version, user tier, and geographic info
			"saas" => (WebApplication)app.UseFeatureFlags(options =>
							{
								// Custom attribute extractors
								options.AttributeExtractors.Add(context =>
								{
									var attributes = new Dictionary<string, object>();
									// Extract tenant information
									if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId))
										attributes["tenantId"] = tenantId.ToString();

									// Extract API version
									if (context.Request.Headers.TryGetValue("X-API-Version", out var version))
										attributes["apiVersion"] = version.ToString();

									// Extract user tier from JWT
									var userTier = context.User.FindFirst("tier")?.Value;
									if (!string.IsNullOrEmpty(userTier))
										attributes["userTier"] = userTier;

									// Extract geographic info (you might use a GeoIP service)
									var country = GetCountryFromIP(context.Connection.RemoteIpAddress);
									if (!string.IsNullOrEmpty(country))
										attributes["country"] = country;
									return attributes;
								});
							}),
			// Advanced middleware configuration example:
			// Feature flag middleware with maintenance mode AND additional attributes from headers
			// that can be useful for targeting rules
			"maintenance+headers" => (WebApplication)app.UseFeatureFlags(options =>
							{
								options.EnableMaintenanceMode = true;
								options.MaintenanceFlagKey = "api-maintenance";
								options.MaintenanceResponse = new
								{
									message = "API is temporarily down for maintenance",
									estimatedDuration = "30 minutes",
									contact = "support@company.com"
								};

								// Custom user ID extraction to support User-Id header
								options.UserIdExtractor = context =>
								{
									// Try User-Id header first
									var userIdHeader = context.Request.Headers["User-Id"].FirstOrDefault();
									if (!string.IsNullOrEmpty(userIdHeader))
										return userIdHeader;

									// Fallback to default extraction
									return context.User.Identity?.Name ??
										   context.Request.Headers["X-User-ID"].FirstOrDefault() ??
										   context.Request.Query["userId"].FirstOrDefault();
								};

								// Extract headers needed for targeting rules
								options.AttributeExtractors.Add(context =>
								{
									var attributes = new Dictionary<string, object>();

									// Extract Role header for admin panel targeting
									if (context.Request.Headers.TryGetValue("Role", out var role))
										attributes["role"] = role.ToString();

									// Extract Department header for admin panel targeting  
									if (context.Request.Headers.TryGetValue("Department", out var department))
										attributes["department"] = department.ToString();

									// Extract User-Type header for recommendation targeting
									if (context.Request.Headers.TryGetValue("User-Type", out var userType))
										attributes["userType"] = userType.ToString();

									// Extract Country header for recommendation targeting
									if (context.Request.Headers.TryGetValue("Country", out var country))
										attributes["country"] = country.ToString();

									// Extract Timezone header for time window features
									if (context.Request.Headers.TryGetValue("Timezone", out var timezone))
										attributes["timezone"] = timezone.ToString();

									return attributes;
								});
							}),
			_ => (WebApplication)app.UseFeatureFlags(),// Simple feature flag middleware with defaults
		};
	}

	static string? GetCountryFromIP(System.Net.IPAddress? ipAddress)
	{
		// In production, use a real GeoIP service like MaxMind
		return ipAddress?.ToString() switch
		{
			var ip when ip.StartsWith("192.168") => "US", // Local dev
			_ => "US" // Default
		};
	}

	// Ensure the feature flags database is fully initialized. Note: In production, it's recommended to use migrations
	public static async Task EnsureFeatureFlagsDatabase(this WebApplication app)
	{
		try
		{
			app.Logger.LogInformation("Starting feature flags database initialization...");

			// This will create the database and tables if they don't exist
			await app.Services.EnsureFeatureFlagDatabase();

			app.Logger.LogInformation("Feature flags database initialization completed successfully");

			// Flag seeding from SQL script file (NOT RECOMMEND FOR PRODUCTION)
			// Use: during application startup at development time
			await app.Services.SeedFeatureFlags("seed-db.sql");
		}
		catch (Exception ex)
		{
			app.Logger.LogError(ex, "Failed to initialize feature flags database. Application will continue but feature flags may not work properly.");

			// Decide whether to continue or exit based on your requirements
			if (app.Environment.IsProduction())
			{
				// In production, you might want to exit if database setup fails
				app.Logger.LogCritical("Exiting application due to database initialization failure in production environment");
				throw; // Re-throw to stop application startup
			}
			// In development/staging, continue running so developers can troubleshoot
		}
	}

	public static async Task DeployFeatureFlagsFromFactory(this WebApplication app)
	{
		var serviceProvider = app.Services.CreateScope().ServiceProvider;
		var repository = serviceProvider.GetRequiredService<IFeatureFlagRepository>();

		// Get all registered feature flags from the factory
		var factory = serviceProvider.GetRequiredService<IFeatureFlagFactory>();
		var allFlags = factory.GetAllFlags();

		// Insert each flag in the database if not already present
		foreach (var flag in allFlags)
		{
			await flag.DeployAsync(repository);
		}
	}

	// Alternative approach:
	// If you don't have a factory or didn't register flags with the container,
	// scan the current assembly for all IRegisteredFeatureFlag implementations
	public static async Task DeployFeatureFlagsFromAssembly(this WebApplication app)
	{
		var serviceProvider = app.Services.CreateScope().ServiceProvider;
		var repository = serviceProvider.GetRequiredService<IFeatureFlagRepository>();

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
