using Propel.ClientApi.FeatureFlags;
using Propel.FeatureFlags.Attributes;

namespace Propel.ClientApi.MinimalApiEndpoints;

public static class NotificationsEndpoints
{
	public static void MapNotificationsEndpoints(this WebApplication app)
	{
		// Example of a feature flag with a decorator pattern
		app.MapGet("/send-email", async (INotificationService svc) =>
		{
			var results = await svc.SendEmailAsync("user123", "Test Subject", "Test Body");
			return Results.Ok(results);
		});
	}
}

public interface INotificationService
{
	Task<string> SendEmailAsync(string userId, string subject, string body);
	Task<string> SendEmailLegacyAsync(string userId, string subject, string body);
}

public class NotificationService(ILogger<NotificationService> logger) : INotificationService
{
	[FeatureFlagged(flagKey: "new-email-service", fallbackMethod: nameof(SendEmailLegacyAsync))]
	public virtual async Task<string> SendEmailAsync(string userId, string subject, string body)
	{
		// New email service implementation
		logger.LogInformation("Using new email service for user {UserId}", userId);
		await Task.Delay(100);

		return "Email sent using new service.";
	}

	[FeatureFlaggedV2(type: typeof(NewEmailServiceFeatureFlag), fallbackMethod: nameof(SendEmailLegacyAsync))]
	public virtual async Task<string> SendEmailV2Async(string userId, string subject, string body)
	{
		// New email service implementation
		logger.LogInformation("Using new email service for user {UserId}", userId);
		await Task.Delay(100);

		return "Email sent using new service.";
	}


	public virtual async Task<string> SendEmailLegacyAsync(string userId, string subject, string body)
	{
		// Legacy email service implementation
		logger.LogInformation("Using legacy email service for user {UserId}", userId);
		await Task.Delay(200);

		return "Email sent using legacy service.";
	}
}
