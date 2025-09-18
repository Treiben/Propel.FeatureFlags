using Propel.ClientApi.FeatureFlags;
using Propel.FeatureFlags.Services.ApplicationScope;

namespace Propel.ClientApi.Services;

// ===== 2. A/B TESTING WITH VARIATIONS =====

public interface IRecommendationService
{
	Task<List<Product>> GetRecommendationsAsync(string userId, string category);
}

// Service demonstrating feature flag call using IFeatureFlagClient instead of HttpContext
public class RecommendationService(IFeatureFlagClient featureFlags, ILogger<RecommendationService> logger) : IRecommendationService
{
	public async Task<List<Product>> GetRecommendationsAsync(string userId, string category)
	{
		var context = new Dictionary<string, object>
		{
			["category"] = category,
			["userSegment"] = await GetUserSegment(userId)
		};

		// A/B test for recommendation algorithm
		var recommendationAlgorithmFlag = new RecommendationAlgorithmFeatureFlag();
		var algorithm = await featureFlags.GetVariationAsync(
				flag: recommendationAlgorithmFlag,
				defaultValue: "collaborative-filtering", // default
				userId: userId,
				attributes: context
			);

		logger.LogInformation("Using recommendation algorithm {Algorithm} for user {UserId}", algorithm, userId);

		return algorithm switch
		{
			"machine-learning" => await GetMLRecommendations(userId, category),
			"content-based" => await GetContentBasedRecommendations(userId, category),
			"collaborative-filtering" => await GetCollaborativeRecommendations(userId, category),
			_ => await GetCollaborativeRecommendations(userId, category)
		};
	}

	private async Task<string> GetUserSegment(string userId) => "premium"; // Placeholder
	private async Task<List<Product>> GetMLRecommendations(string userId, string category) => new();
	private async Task<List<Product>> GetContentBasedRecommendations(string userId, string category) => new();
	private async Task<List<Product>> GetCollaborativeRecommendations(string userId, string category) => new();
}

public class Product
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}
