using DemoWebApi;
using DemoWebApi.MinimalApiEndpoints;
using DemoWebApi.Services;
using Propel.FeatureFlags.Attributes.Extensions;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();


//-----------------------------------------------------------------------------
// Configure Propel FeatureFlags
//-----------------------------------------------------------------------------
builder.ConfigureFeatureFlags(config =>
{
	config.RegisterFlagsWithContainer = true;				// automatically register all flags in the assembly with the DI container
	config.EnableFlagFactory = true;						// enable IFeatureFlagFactory for type-safe flag access

	config.SqlConnection = 
		builder.Configuration
				.GetConnectionString("DefaultConnection")!; // PostgreSQL connection string

	config.Cache = new CacheOptions							// Configure caching (optional, but recommended for performance and scalability)
	{
		EnableDistributedCache = true,
		Connection = builder.Configuration.GetConnectionString("RedisConnection")!,
	};
	
	var interception = config.Interception;
	interception.EnableHttpIntercepter = true;			// automatically add interceptors for attribute-based flags

});

// optional: register your services with methods decorated with [FeatureFlagged] attribute
builder.Services.RegisterWithFeatureFlagInterception<INotificationService, NotificationService>();

//-----------------------------------------------------------------------------
// Register your application services normally

builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IPaymentProcessorV1, PaymentProcessorV1>();
builder.Services.AddScoped<IPaymentProcessorV2, PaymentProcessorV2>();
builder.Services.AddScoped<PaymentService>();

var app = builder.Build();

// optional: ensure the feature flags database exists and schema is created
if (app.Environment.IsDevelopment())
{
	await app.InitializeFeatureFlagsDatabase();
}

// recommended: automatically add flags in the database at startup if they don't exist
await app.AutoDeployFlags();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapHealthChecks("/health");

// optional:add the feature flag middleware to the pipeline for global flags evaluation and to extract evaluation context from request paths or headers
app.AddFeatureFlagMiddleware("maintenance+headers"); // example scenarios: "basic", "saas", "maintenance", "global", "user-extraction", "headers"

app.MapAdminEndpoints();
app.MapNotificationsEndpoints();
app.MapOrderEndpoints();
app.MapProductEndpoints();
app.MapRecommendationsEndpoints();

app.Run();

