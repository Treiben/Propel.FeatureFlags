using Propel.FeatureFlags.Domain;

namespace DemoLegacyApi.FeatureFlags.ApplicationFlags
{
	public class RecommendationAlgorithmFeatureFlag : FeatureFlagBase
	{
		public RecommendationAlgorithmFeatureFlag()
			: base(
				key: "recommendation-algorithm",
				name: "Recommendation Algorithm",
				description: "Controls recommendation algorithm (50% rollout)",
				onOfMode: EvaluationMode.Off)
		{
		}
	}
}