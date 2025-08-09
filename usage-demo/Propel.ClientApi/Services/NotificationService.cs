using FeatureToggles.Core;

namespace FeatureToggles.Demo.Api.Services;

// Usage example with decorator
public interface INotificationService
{
	Task SendEmailAsync(string userId, string subject, string body);
	Task SendEmailLegacyAsync(string userId, string subject, string body);
}

public class NotificationService(ILogger<NotificationService> logger) : INotificationService
{
	[FeatureFlagged("new-email-service", fallbackMethod: nameof(SendEmailLegacyAsync))]
	public async Task SendEmailAsync(string userId, string subject, string body)
	{
		// New email service implementation
		logger.LogInformation("Using new email service for user {UserId}", userId);
		await Task.Delay(100);
	}

	public async Task SendEmailLegacyAsync(string userId, string subject, string body)
	{
		// Legacy email service implementation
		logger.LogInformation("Using legacy email service for user {UserId}", userId);
		await Task.Delay(200);
	}
}
