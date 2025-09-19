using Propel.FeatureFlags.Domain;

namespace ApiFlagUsageDemo.FeatureFlags;

public class RecommendationAlgorithmFeatureFlag : FeatureFlagBase
{
	public RecommendationAlgorithmFeatureFlag()
		: base(key: "recommendation-algorithm",
			name: "Recommendation Algorithm",
			description: "Controls which recommendation algorithm implementation is used for generating user recommendations. Supports variations including machine-learning, content-based, and collaborative-filtering algorithms. Enables A/B testing of different technical approaches while maintaining consistent business functionality.",
			onOfMode: EvaluationMode.On) // Flag will be created and immediately enabled if it does not already exist in the database (not recommended).
										 // It is often safer to default to disabled so when the feature is deployed it can be enabled from management website.
	{
	}
}
