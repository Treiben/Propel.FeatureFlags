using Microsoft.Extensions.Diagnostics.HealthChecks;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.PostgresSql;
using Propel.FeatureFlags.Redis;
using Propel.FlagsManagement.Api;
using Propel.FlagsManagement.Api.Endpoints.Shared;
using Propel.FlagsManagement.Api.Healthchecks;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddHttpContextAccessor();

// Add CORS configuration
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowFrontend", policy =>
	{
		policy.WithOrigins(
				"http://localhost:3000",  // React dev server
				"https://localhost:3000", // React dev server HTTPS
				"http://localhost:5173",  // Vite default port
				"https://localhost:5173"  // Vite default port HTTPS
			)
			.AllowAnyMethod()
			.AllowAnyHeader()
			.AllowCredentials();
	});

	// Allow all origins for development (less secure, use only for dev)
	options.AddPolicy("AllowAll", policy =>
	{
		policy.AllowAnyOrigin()
			.AllowAnyMethod()
			.AllowAnyHeader();
	});
});

// Configure JSON serialization options for HTTP endpoints (Minimal APIs)
builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
	options.SerializerOptions.WriteIndented = true;
	options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
	options.SerializerOptions.Converters.Add(new CustomJsonConverter<FlagEvaluationMode>());
	options.SerializerOptions.Converters.Add(new CustomJsonConverter<DayOfWeek>());
	options.SerializerOptions.Converters.Add(new CustomJsonConverter<TargetingOperator>());
});

builder.Services.AddAuthentication("Bearer").AddJwtBearer();
builder.Services.AddAuthorizationBuilder()
	.AddPolicy("ApiScope", policy =>
	{
		policy.RequireAuthenticatedUser();
		policy.RequireClaim("scope", "propel-management-api");
	})
	.AddFallbackPolicy("RequiresReadRights", AuthorizationPolicies.HasReadActionPolicy);

// Configure feature flags
var featureFlagOptions = builder.Configuration.GetSection("PropelFeatureFlags").Get<FeatureFlagConfigurationOptions>() ?? new();
builder.Services.AddFeatureFlags(featureFlagOptions);
builder.Services.AddPostgresSqlFeatureFlags(featureFlagOptions.SqlConnectionString);
builder.Services.AddRedisCache(featureFlagOptions.RedisConnectionString);

// Configure flags management api services
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddValidators();
builder.Services.AddHandlers();
builder.Services.RegisterApplicationEndpoints();

// Add health checks with proper fallback handling
var healthChecksBuilder = builder.Services.AddHealthChecks();

// Add liveness check (always available)
healthChecksBuilder.AddCheck("self", () => HealthCheckResult.Healthy("Application is running"), tags: ["liveness"]);

// Add PostgreSQL health check only if connection string is available
if (!string.IsNullOrEmpty(featureFlagOptions.SqlConnectionString))
{
	healthChecksBuilder.AddNpgSql(
		connectionString: featureFlagOptions.SqlConnectionString,
		healthQuery: "SELECT 1;",
		name: "postgres",
		failureStatus: HealthStatus.Unhealthy,
		tags: ["database", "postgres", "readiness"]);
}

// Add Redis health check only if connection string is available
if (!string.IsNullOrEmpty(featureFlagOptions.RedisConnectionString))
{
	healthChecksBuilder.AddRedis(
		redisConnectionString: featureFlagOptions.RedisConnectionString,
		name: "redis",
		failureStatus: HealthStatus.Degraded,
		tags: ["cache", "redis", "readiness"]);
}

var app = builder.Build();

await app.InitializeFeatureFlagsDatabase();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Use CORS - must be before UseAuthentication and UseAuthorization
if (app.Environment.IsDevelopment())
{
	app.UseCors("AllowAll"); // More permissive for development
}
else
{
	app.UseCors("AllowFrontend"); // Restricted for production
}

app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthCheckEndpoints();
app.MapApplicationEndpoints();

app.Run();

public static class AppExtensions
{
	public static async Task InitializeFeatureFlagsDatabase(this WebApplication app)
	{
		try
		{
			app.Logger.LogInformation("Starting feature flags database initialization...");

			// This will create the database and tables if they don't exist
			await app.Services.EnsureFeatureFlagsDatabaseAsync();

			app.Logger.LogInformation("Feature flags database initialization completed successfully");
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
}