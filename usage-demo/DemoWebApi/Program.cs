using DemoWebApi;
using DemoWebApi.MinimalApiEndpoints;
using DemoWebApi.Services;
using Propel.FeatureFlags.Attributes.Extensions;
using Propel.FeatureFlags.PostgreSql.Extensions;
using Propel.FeatureFlags.Redis;
using Propel.FeatureFlags.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();


//-----------------------------------------------------------------------------
// Configure Propel FeatureFlags
//-----------------------------------------------------------------------------
builder.Services
	.ConfigureFeatureFlags(config =>
		{
			config.RegisterFlagsWithContainer = true;	// automatically register all flags in the assembly with the DI container
			config.EnableFlagFactory = true;			// enable IFeatureFlagFactory for type-safe flag access

			var interception = config.Interception;
			interception.EnableHttpIntercepter = true;	// automatically add interceptors for attribute-based flags
		})
	.AddPostgreSqlFeatureFlags(builder.Configuration.GetConnectionString("DefaultConnection")!)
	.AddRedisCache(builder.Configuration.GetConnectionString("RedisConnection")!, 
		options =>
		{
			options.EnableInMemoryCache = true;         // Enable local in-memory cache as first-level cache
			options.CacheDurationInMinutes = 180;		// 3 hours for stable production
			options.LocalCacheSizeLimit = 2000;			// Support more flags
			options.LocalCacheDurationSeconds = 15;		// Slightly longer local cache
			options.CircuitBreakerThreshold = 5;		// More tolerant in production
			options.RedisTimeoutMilliseconds = 7000;	// Longer timeout for remote Redis		
		});  // Configure caching (optional, but recommended for performance and scalability)

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
//app.AddFeatureFlagMiddleware("maintenance+headers"); // example scenarios: "basic", "saas", "maintenance", "global", "user-extraction", "headers"
app.AddFeatureFlagMiddleware("basic");

app.MapAdminEndpoints();
app.MapNotificationsEndpoints();
app.MapOrderEndpoints();
app.MapProductEndpoints();
app.MapRecommendationsEndpoints();

app.Run();

