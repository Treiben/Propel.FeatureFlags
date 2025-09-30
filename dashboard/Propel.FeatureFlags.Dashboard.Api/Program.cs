using Propel.FeatureFlags.Dashboard.Api;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Healthchecks;
using Propel.FeatureFlags.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddHttpContextAccessor();

// Configure dashboard-specific services
builder.ConfigureFeatureFlags(options =>
{
	options.Cache = new CacheOptions            // Configure caching (optional, but recommended for performance and scalability)
	{
		EnableInMemoryCache = false,
		EnableDistributedCache = false,
		Connection = builder.Configuration.GetConnectionString("RedisConnection")!,
	};

	options.Database.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
});

// Configure CORS policies
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

// Configure authentication and authorization
builder.Services.AddAuthentication("Bearer").AddJwtBearer();
builder.Services.AddAuthorizationBuilder()
	.AddPolicy("ApiScope", policy =>
	{
		policy.RequireAuthenticatedUser();
		policy.RequireClaim("scope", "propel-dashboard-api");
	})
	.AddFallbackPolicy("RequiresReadRights", AuthorizationPolicies.HasReadActionPolicy);

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

app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthCheckEndpoints();
app.MapDashboardEndpoints();

app.Run();