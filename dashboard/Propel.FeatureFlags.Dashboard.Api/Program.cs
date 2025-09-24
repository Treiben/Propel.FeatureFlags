using Propel.FeatureFlags.Dashboard.Api;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Healthchecks;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;
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
	options.SerializerOptions.Converters.Add(new EnumJsonConverter<EvaluationMode>());
	options.SerializerOptions.Converters.Add(new EnumJsonConverter<DayOfWeek>());
	options.SerializerOptions.Converters.Add(new EnumJsonConverter<TargetingOperator>());
});

builder.Services.AddAuthentication("Bearer").AddJwtBearer();
builder.Services.AddAuthorizationBuilder()
	.AddPolicy("ApiScope", policy =>
	{
		policy.RequireAuthenticatedUser();
		policy.RequireClaim("scope", "propel-management-api");
	})
	.AddFallbackPolicy("RequiresReadRights", AuthorizationPolicies.HasReadActionPolicy);

// Configure dashboard-specific services
var propelOptions = builder.Configuration.GetSection("PropelOptions").Get<PropelOptions>() ?? new();

builder.Services.AddFeatureFlagCoreServices(propelOptions);

builder.Services
	.AddDashboardPersistence(propelOptions)
	.AddDashboardServices()
	.AddDashboardHealthchecks(propelOptions);

var app = builder.Build();

await app.EnsureDatabase();

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
app.MapDashboardEndpoints();

app.Run();

public static class AppExtensions
{
	public static async Task EnsureDatabase(this WebApplication app)
	{
		try
		{
			app.Logger.LogInformation("Starting feature flags database initialization...");

			// This will create the database and tables if they don't exist
			await app.Services.EnsureFeatureFlagDatabase();

			app.Logger.LogInformation("Feature flags database initialization completed successfully");

			// Flag seeding from SQL script file (NOT RECOMMEND FOR PRODUCTION)
			// Use: during application startup at development time
			if (app.Environment.IsDevelopment())
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
}