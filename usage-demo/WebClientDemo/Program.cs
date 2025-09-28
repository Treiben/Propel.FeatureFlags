using ApiFlagUsageDemo.FeatureFlags;
using ApiFlagUsageDemo.MinimalApiEndpoints;
using ApiFlagUsageDemo.Services;
using Propel.FeatureFlags.Extensions;

using Propel.FeatureFlags.Infrastructure;
using WebClientDemo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();


//-----------------------------------------------------------------------------
// Configure Propel FeatureFlags
//-----------------------------------------------------------------------------
builder.ConfigureFeatureFlags(options =>
{
	options.RegisterFlagsWithContainer = true;	// automatically register all flags in the assembly with the DI container
	options.InsertFlagsInDatabase = true;       // automatically insert flags in the database at startup if they don't exist

	options.Cache = new CacheOptions			// Configure caching (optional, but recommended for performance and scalability)
	{
		EnableInMemoryCache = false,
		EnableDistributedCache = false,
		Connection = builder.Configuration.GetConnectionString("RedisConnection")!,
	};

	options.Database.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!; 
	options.AttributeIntercepting.EnableHttpIntercepter = true; // automatically add interceptors for attribute-based flags
});

// optional: register your services with methods decorated with [FeatureFlagged] attribute
builder.Services.RegisterServiceWithFlagAttributes<INotificationService, NotificationService>();

//optional: for large code bases with tons of flags, you might want to implement your own feature flag factory
builder.Services.AddSingleton<IFeatureFlagFactory, DemoFeatureFlagFactory>();

//-----------------------------------------------------------------------------
// Register your application services normally

builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IPaymentProcessorV1, PaymentProcessorV1>();
builder.Services.AddScoped<IPaymentProcessorV2, PaymentProcessorV2>();
builder.Services.AddScoped<PaymentService>();

var app = builder.Build();

// optional: ensure the feature flags database is initialized and feature flags are registered
if (app.Environment.IsDevelopment())
{
	await app.EnsureFeatureFlagsDatabase();
}

// optional: deploy feature flags to the database (RECOMMENDED)
await app.DeployFeatureFlagsFromFactory();

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

