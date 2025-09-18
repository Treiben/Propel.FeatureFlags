using Propel.ClientApi.FeatureFlags;
using Propel.ClientApi.MinimalApiEndpoints;
using Propel.ClientApi.Services;
using Propel.FeatureFlags;
using Propel.FeatureFlags.AspNetCore.Middleware;
using Propel.FeatureFlags.Attributes;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;
using Propel.FeatureFlags.Infrastructure.Redis;
using Propel.FeatureFlags.Services.ApplicationScope;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();


//-----------------------------------------------------------------------------
// Configure Propel FeatureFlags
//-----------------------------------------------------------------------------
var options = builder.Configuration.GetSection("PropelFeatureFlags").Get<FeatureFlagConfigurationOptions>() ?? new();

// Register the core feature flag services
builder.Services.AddFeatureFlagServices(options);

// Register the feature flags defined in the assembly (RECOMMENDED)
builder.Services.AddAllFeatureFlags();

//optional: for large code bases with tons of flags, you might want to implement your own feature flag factory
builder.Services.AddSingleton<IFeatureFlagFactory, DemoFeatureFlagFactory>();

//-----------------------------------------------------------------------------
// Optional integrations
//-----------------------------------------------------------------------------

// STORAGE
// If you want to use PostgreSQL for feature flag storage, uncomment the line below
// 0. Add the Propel.FeatureFlags.PostgresSql package
// 1. Add the PostgreSQL connection string to appsettings.json
var postgresConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
	?? options.SqlConnectionString // fallback to the configured SQL connection string
	?? throw new InvalidOperationException("PostgreSQL connection string is required but not found in configuration");

builder.Services.AddPostgresSqlFeatureFlags(postgresConnectionString);
// Otherwise,you can follow the similar steps to use other storage options like MongoDB, SQL Server, Azure AppConfiguration, etc.
//-----------------------------------------------------------------------------

// CACHING
// If you want to use Redis for caching feature flags, uncomment the line below
// 0. Add the Propel.FeatureFlags.Redis package
// 1. Add the Redis connection string to appsettings.json
// 2. Register the Redis cache
if (options.CacheOptions.UseCache == true && !string.IsNullOrEmpty(options.RedisConnectionString))
	builder.Services.AddRedisCache(options.RedisConnectionString);
// If featureFlagOptions.UseCache is true and Redis is not configured, then it will use in-memory caching by default
// If you don't want any caching, set UseCache to false in appsettings.json


//-----------------------------------------------------------------------------
// ATTRIBUTE-BASED FEATURE FLAGS VIA ATTRIBUTES (AOP EXPERIMENTAL)
//-----------------------------------------------------------------------------
// To use FeatureFlaggedAttribute:
// 0. Add the Propel.FeatureFlags.Attributes package
// 1. Register the attributes
// option 1: If the flags don't need to use any HttpContext data or is not aspnet application, register as
// builder.Services.AddFeatureFlagsAttributes();
// option 2: if you have custom data that should be passed from HttpContext headers (e.g. user id, tenant id), use
builder.Services.AddHttpFeatureFlagsAttributes(); // HttpContextAccessor must be added (e.g. builder.Services.AddHttpContextAccessor)

//2. Register the service with interceptor to enable attribute-based feature flags
builder.Services.AddScopedWithFeatureFlags<INotificationService, NotificationService>();

//-----------------------------------------------------------------------------

builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<PaymentService>();

var app = builder.Build();

//-----------------------------------------------------------------------------
// Ensure the feature flags database is initialized and feature flags are registered
await app.InitializeFeatureFlagsDatabase(sqlScriptFile: "init-db.sql");
//-----------------------------------------------------------------------------

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapHealthChecks("/health");

//-----------------------------------------------------------------------------
// Examples of setting up feature flag middleware for different usage scenarios
app.MapFeatureFlagMiddleware("maintenanceWithHeaders");
//-----------------------------------------------------------------------------

app.MapAdminEndpoints();
app.MapNotificationsEndpoints();
app.MapOrderEndpoints();
app.MapProductEndpoints();
app.MapRecommendationsEndpoints();

app.Run();

public static class AppExtensions
{
	public static WebApplication MapFeatureFlagMiddleware(this WebApplication app, string scenario = "basic")
	{
		switch (scenario)
		{
			// Simple middleware configuration example:
			// Feature flag middleware with maintenance mode enabled - global kill mode that'll shut down the API
			// This will automatically check the "api-maintenance" flag and return a 503 Service Unavailable response if enabled
			case "maintenance":
				return (WebApplication)app.UseFeatureFlags(options =>
				{
					options.EnableMaintenanceMode = true;
					options.MaintenanceFlagKey = "api-maintenance";
					options.MaintenanceResponse = new
					{
						message = "API is temporarily down for maintenance",
						estimatedDuration = "30 minutes",
						contact = "support@company.com"
					};

				});

			// Slightly more complex middleware configuration example:
			// Feature flag middleware with global flags
			// This allows you to define global feature gates that apply to all users
			case "globalFlags":

				return (WebApplication)app.UseFeatureFlags(options =>
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
				});

			// For more advanced feature flags, such as A/B or user percentage,
			// example of feature flag middleware with custom user ID extraction and attribute extractors
			case "customUserExtraction":
				return (WebApplication)app.UseFeatureFlags(options =>
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
				});

			// Example of feature flag middleware configuration for SaaS with custom attribute extractors
			// for example, extracting tenant information, API version, user tier, and geographic info
			case "saas":
				return (WebApplication)app.UseFeatureFlags(options =>
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
				});

			// Advanced middleware configuration example:
			// Feature flag middleware with maintenance mode AND additional attributes from headers
			// that can be useful for targeting rules
			case "maintenanceWithHeaders":
				return (WebApplication)app.UseFeatureFlags(options =>
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
				});


			default:
				// Simple feature flag middleware with defaults
				return (WebApplication)app.UseFeatureFlags();
		}
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

	// Ensure the feature flags database is initialized and feature flags are registered
	public static async Task InitializeFeatureFlagsDatabase(this WebApplication app, string? sqlScriptFile = null)
	{
		try
		{
			app.Logger.LogInformation("Starting feature flags database initialization...");

			// This will create the database and tables if they don't exist
			await app.Services.EnsureFeatureFlagsDatabaseAsync();

			app.Logger.LogInformation("Feature flags database initialization completed successfully");

			if (!app.Environment.IsProduction() && !string.IsNullOrEmpty(sqlScriptFile))
			{
				// Flags should be defined in the provided SQL script file
				// Use: during application startup at development time
				// NOT RECOMMEND FOR PRODUCTION
				// For production, use migrations instead or seed with registered flags from assembly
				await app.Services.SeedDatabaseFromScriptAsync(sqlScriptFile);
			}

			// seed with registered flags from assembly
			await app.DeployFlagsAsync();
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

	private static async Task DeployFlagsAsync(this WebApplication app)
	{
		var serviceProvider = app.Services.CreateScope().ServiceProvider;
		var repository = serviceProvider.GetRequiredService<IFlagEvaluationRepository>();

		//------------------------------------------------------------------------
		// Insert new flags 
		//-------------------------------------------------------------------------
		var factory = serviceProvider.GetRequiredService<IFeatureFlagFactory>();
		var allFlags = factory.GetAllFlags();

		foreach (var flag in allFlags)
		{
			await flag.EnsureFeatureFlagsInDatabaseAsync(repository);
		}

		//------------------------------------------------------------------------
		// Alternative approach:
		// If you don't have a factory or any flag reposiotry, you can
		// scan the current assembly for all IRegisteredFeatureFlag implementations

		//var currentAssembly = Assembly.GetExecutingAssembly();
		//var allFlags = currentAssembly
		//	.GetTypes()
		//	.Where(t => typeof(IRegisteredFeatureFlag).IsAssignableFrom(t)
		//			&& !t.IsInterface
		//			&& !t.IsAbstract);
		//foreach (var flag in allFlags)
		//{
		//	var instance = (IRegisteredFeatureFlag)Activator.CreateInstance(flag)!;
		//	await instance.EnsureFeatureFlagsInDatabaseAsync(repository);
		//}
		//------------------------------------------------------------------------
	}
}

