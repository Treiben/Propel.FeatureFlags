using Propel.FeatureFlags.Core;

namespace Propel.ClientApi.FeatureFlags;

public class RecommendationAlgorithmFeatureFlag : TypeSafeFeatureFlag
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
			isEnabledOnCreation: true)
	{
	}
}
