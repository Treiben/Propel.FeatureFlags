using Propel.ClientApi.MinimalApiEndpoints;
using Propel.ClientApi.Services;
using Propel.FeatureFlags;
using Propel.FeatureFlags.AspNetCore.Middleware;
using Propel.FeatureFlags.Attributes;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.PostgresSql;
using Propel.FeatureFlags.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddHealthChecks();

var featureFlagOptions = builder.Configuration.GetSection("PropelFeatureFlags").Get<FlagOptions>() ?? new();

// Register the core feature flag services
builder.Services.AddFeatureFlags(featureFlagOptions);

// If you want to use PostgreSQL for feature flag storage, uncomment the line below
// 0. Add the FeatureToggles.Persistence.PostgresSQL package
// 1. Add the PostgreSQL connection string to appsettings.json
builder.Services.AddPostgresSqlFeatureFlags(featureFlagOptions.SqlConnectionString);
// Otherwise,you can follow the similar steps to use other storage options like MongoDB, SQL Server, Azure AppConfiguration, etc.

// If you want to use Redis for caching feature flags, uncomment the line below
// 0. Add the FeatureToggles.Cache.Redis package
// 1. Add the Redis connection string to appsettings.json
// 2. Register the Redis cache
if (!string.IsNullOrEmpty(featureFlagOptions.RedisConnectionString))
	builder.Services.AddRedisCache(featureFlagOptions.RedisConnectionString);
// Otherwise, it will use in-memory caching by default

// To use FeatureFlaggedAttribute:
// 0. Add the FeatureToggles.Attributes package
// 1. Register the attributes
builder.Services.AddFeatureFlagsAttributes(builder.Configuration);
// 2. Register implementation only
builder.Services.AddScoped<NotificationService>();
// 3. Register the implementation of the interface with the interceptor
builder.Services.RegisterService<INotificationService, NotificationService>();
// Otherwise, register the service normally without feature flags attbutes
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

// Examples of setting up feature flag middleware for different usage scenarios
app.MapFeatureFlagMiddleware("maintenance"); //usage scenarios: basic, maintenance, globalFlags, customUserExtraction, saas

app.MapAdminEndpoints();
app.MapNotificationsEndpoints();
app.MapOrderEndpoints();
app.MapProductEndpoints();
app.MapRecommendationsEndpoints();

app.Run();

public static class  AppExtensions
{
	public static WebApplication MapFeatureFlagMiddleware(this WebApplication app, string scenario = "basic")
	{
		switch (scenario)
		{
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

			// Feature flag middleware with global flags
			// This allows you to define global feature gates that apply to all users
			case "globalFlags":

				return (WebApplication)app.UseFeatureFlags(options =>
				{
					// Add global feature gates
					options.GlobalFlags.Add(new GlobalFlag
					{
						FlagKey = "api-v2-enabled",
						StatusCode = 410, // Gone
						Response = new { error = "API v2 is no longer available, please upgrade to v3" }
					});
					options.GlobalFlags.Add(new GlobalFlag
					{
						FlagKey = "beta-features-enabled",
						StatusCode = 403, // Forbidden
						Response = new { error = "Beta features not available for your account" }
					});
				});

			// Feature flag middleware with custom user ID extraction and attribute extractors
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

			// Feature flag middleware with custom attribute extractors
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
}

