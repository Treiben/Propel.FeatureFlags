using Propel.FeatureFlags.Domain;

namespace DemoLegacyApi.FeatureFlags.ApplicationFlags
{
	public class NewProductApiFeatureFlag : FeatureFlagBase
	{
		public NewProductApiFeatureFlag()
			: base(
				key: "new-product-api",
				name: "New Product API",
				description: "Controls Product API versioning (v1 vs v2)",
				onOfMode: EvaluationMode.On)
		{
		}
	}
}