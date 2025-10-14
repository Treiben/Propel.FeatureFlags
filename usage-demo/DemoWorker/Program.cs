using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Propel.FeatureFlags.Attributes;
using Propel.FeatureFlags.Attributes.Extensions;
using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.SqlServer.Extensions;
using Propel.FeatureFlags.DependencyInjection.Extensions;

//-----------------------------------------------------------------------------
// Build Host with Configuration
//-----------------------------------------------------------------------------
var builder = Host.CreateApplicationBuilder(args);

// Add configuration from appsettings.json
builder.Configuration
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
	.AddEnvironmentVariables();

// Add logging
builder.Services.AddLogging(config =>
{
	config.AddConsole();
	config.AddDebug();
	config.SetMinimumLevel(LogLevel.Debug);
});

//-----------------------------------------------------------------------------
// Configure Propel FeatureFlags
//-----------------------------------------------------------------------------
builder.Services
	.ConfigureFeatureFlags(config =>
		{
			config.RegisterFlagsWithContainer = true;						// automatically register all flags in the assembly with the DI container
			config.EnableFlagFactory = true;								// enable IFeatureFlagFactory for type-safe flag access


			config.LocalCacheConfiguration = new LocalCacheConfiguration	// Configure caching (optional, but recommended for performance and scalability)
			{
				LocalCacheEnabled = true,
				CacheDurationInMinutes = 10,								// short local cache duration for better consistency,
				CacheSizeLimit = 1000                                       // limit local cache size to prevent memory bloat
			};

			var interception = config.Interception;
			// Note: For console apps, use EnableIntercepter instead of EnableHttpIntercepter
			interception.EnableIntercepter = true;							 // enable basic attribute interception (non-HTTP)
		})
	.AddSqlServerFeatureFlags(builder.Configuration.GetConnectionString("DefaultConnection")!);

// Register your services with methods decorated with [FeatureFlagged] attribute
builder.Services.RegisterWithFeatureFlagInterception<INotificationService, NotificationService>();

// Register the background worker
builder.Services.AddHostedService<DemoWorker>();

//-----------------------------------------------------------------------------
// Build and Run
//-----------------------------------------------------------------------------
var app = builder.Build();

// Ensure the feature flags database exists and schema is created
if (app.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
{
	await app.InitializeFeatureFlagsDatabase();
}

// Automatically add flags in the database at startup if they don't exist
await app.AutoDeployFlags();

Console.WriteLine("Console Application Demo Started");
Console.WriteLine("Press Ctrl+C to exit");

await app.RunAsync();

//-----------------------------------------------------------------------------
// Demo Services
//-----------------------------------------------------------------------------

// Feature Flag Definition
public class NewEmailServiceFeatureFlag : FeatureFlagBase
{
	public NewEmailServiceFeatureFlag()
		: base(
			key: "new-email-service",
			name: "New Email Service",
			description: "Controls whether to use the new email service implementation",
			onOfMode: EvaluationMode.Off)  // Start disabled by default
	{
	}
}

// Service Interface
public interface INotificationService
{
	Task<string> SendEmailAsync(string userId, string subject, string body);
	Task<string> SendEmailLegacyAsync(string userId, string subject, string body);
}

// Service Implementation with Feature Flag
public class NotificationService(ILogger<NotificationService> logger) : INotificationService
{
	[FeatureFlagged(type: typeof(NewEmailServiceFeatureFlag), fallbackMethod: nameof(SendEmailLegacyAsync))]
	public virtual async Task<string> SendEmailAsync(string userId, string subject, string body)
	{
		// New email service implementation
		logger.LogInformation("✅ Using NEW email service for user {UserId}", userId);
		await Task.Delay(100);
		return $"✉️ Email sent using NEW service to {userId}";
	}

	public virtual async Task<string> SendEmailLegacyAsync(string userId, string subject, string body)
	{
		// Legacy email service implementation
		logger.LogInformation("⚠️ Using LEGACY email service for user {UserId}", userId);
		await Task.Delay(200);
		return $"📧 Email sent using LEGACY service to {userId}";
	}
}

// Background Worker for Demo - FIXED: Now uses IServiceScopeFactory
public class DemoWorker(
	ILogger<DemoWorker> logger,
	IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		logger.LogInformation("🚀 Demo Worker Started");

		// Give the app time to fully initialize
		await Task.Delay(2000, stoppingToken);

		var iteration = 0;

		while (!stoppingToken.IsCancellationRequested)
		{
			iteration++;
			logger.LogInformation("\n═══════════════════════════════════════════════════");
			logger.LogInformation("🔄 Iteration {Iteration}", iteration);
			logger.LogInformation("═══════════════════════════════════════════════════");

			try
			{
				// Create a new scope for each iteration to resolve scoped services
				using var scope = serviceScopeFactory.CreateScope();
				var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
				var flagClient = scope.ServiceProvider.GetRequiredService<IApplicationFlagClient>();
				var flagFactory = scope.ServiceProvider.GetService<IFeatureFlagFactory>();

				// Demo 1: Using attribute-based feature flagging
				logger.LogInformation("\n📍 Demo 1: Attribute-Based Feature Flagging");
				var result1 = await notificationService.SendEmailAsync(
					$"user{iteration}",
					"Test Subject",
					"Test Body");
				logger.LogInformation("Result: {Result}", result1);

				await Task.Delay(1000, stoppingToken);

				// Demo 2: Direct flag evaluation
				logger.LogInformation("\n📍 Demo 2: Direct Flag Evaluation");
				
				var flag = flagFactory?.GetFlagByType<NewEmailServiceFeatureFlag>() 
					?? new NewEmailServiceFeatureFlag();
				
				var isEnabled = await flagClient.IsEnabledAsync(flag);
				
				logger.LogInformation("Flag 'new-email-service' is currently: {Status}", 
					isEnabled ? "✅ ENABLED" : "❌ DISABLED");

				// Demo 3: Show current flag state
				logger.LogInformation("\n📍 Demo 3: Flag Evaluation Details");
				var evalResult = await flagClient.EvaluateAsync(flag);
				if (evalResult != null)
				{
					logger.LogInformation("Flag: {Key}", flag.Key);
					logger.LogInformation("Enabled: {IsEnabled}", evalResult.IsEnabled);
					logger.LogInformation("Reason: {Reason}", evalResult.Reason);
					logger.LogInformation("Variation: {Variation}", evalResult.Variation);
				}

				// Wait before next iteration
				logger.LogInformation("\n⏱️ Waiting 10 seconds before next iteration...");
				await Task.Delay(10000, stoppingToken);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "❌ Error during demo execution");
				await Task.Delay(5000, stoppingToken);
			}
		}

		logger.LogInformation("🛑 Demo Worker Stopped");
	}
}
