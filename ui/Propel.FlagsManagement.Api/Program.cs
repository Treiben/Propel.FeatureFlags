using FeatureRabbit.Flags;
using FeatureRabbit.Flags.Cache.Redis;
using FeatureRabbit.Flags.Core;
using FeatureRabbit.Flags.Persistence.PostgresSQL;
using FeatureRabbit.Management.Api.Endpoints;
using FeatureRabbit.Management.Api.Endpoints.Shared;
using FeatureRabbit.Management.Api.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var featureFlagOptions = builder.Configuration.GetSection("FeatureRabbit").Get<FlagOptions>() ?? new();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
	options.SerializerOptions.Converters.Add(new CustomJsonConverter<FeatureFlagStatus>());
	options.SerializerOptions.Converters.Add(new CustomJsonConverter<DayOfWeek>());
	options.SerializerOptions.Converters.Add(new CustomJsonConverter<TargetingOperator>());
});

builder.Services.AddAuthentication("Bearer").AddJwtBearer();
builder.Services.AddAuthorizationBuilder()
	.AddPolicy("ApiScope", policy =>
	{
		policy.RequireAuthenticatedUser();
		policy.RequireClaim("scope", "featuretogglesmanagementapi");
	});

builder.Services.AddFeatureFlags(featureFlagOptions);
builder.Services.AddFeatureFlagsPostgresRepository(featureFlagOptions);
builder.Services.AddFeatureFlagsRedisCache(featureFlagOptions);

builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddValidators();
builder.Services.AddHandlers();
builder.Services.RegisterApplicationEndpoints();

// Add health checks with proper fallback handling
var healthChecksBuilder = builder.Services.AddHealthChecks();

// Add liveness check (always available)
healthChecksBuilder.AddCheck("self", () => HealthCheckResult.Healthy("Application is running"), tags: ["liveness"]);

// Add PostgreSQL health check only if connection string is available
if (!string.IsNullOrEmpty(featureFlagOptions.SQLConnectionString))
{
	healthChecksBuilder.AddNpgSql(
		connectionString: featureFlagOptions.SQLConnectionString,
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

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthCheckEndpoints();
app.MapApplicationEndpoints();

app.Run();