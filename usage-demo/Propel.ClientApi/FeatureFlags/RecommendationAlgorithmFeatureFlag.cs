using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Services.ApplicationScope;

namespace Propel.ClientApi.FeatureFlags;

public class RecommendationAlgorithmFeatureFlag : RegisteredFeatureFlag
{
	public RecommendationAlgorithmFeatureFlag()
		: base(key: "recommendation-algorithm",
			name: "Recommendation Algorithm",
			description: "Controls which recommendation algorithm implementation is used for generating user recommendations. Supports variations including machine-learning, content-based, and collaborative-filtering algorithms. Enables A/B testing of different technical approaches while maintaining consistent business functionality.",
			tags: new Dictionary<string, string>
			{
				{ "category", "algorithm" },
				{ "type", "variation-test" },
				{ "impact", "medium" },
				{ "team", "recommendations" },
				{ "variations", "ml,content-based,collaborative-filtering" },
				{ "default", "collaborative-filtering" }
			},
			defaultMode: EvaluationMode.Enabled) // Flag will be created and immediately enabled if it does not already exist in the database (not recommended).
												 // It is often safer to default to disabled so when the feature is deployed it can be enabled from management website.
	{
	}
}
