using Propel.FeatureFlags.Domain;

namespace DemoLegacyApi.FeatureFlags
{
	//=================================================================================
	// Feature Flag Definitions
	//=================================================================================

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

	public class AdminPanelEnabledFeatureFlag : FeatureFlagBase
	{
		public AdminPanelEnabledFeatureFlag()
			: base(
				key: "admin-panel-enabled",
				name: "Admin Panel Access",
				description: "Controls admin panel access",
				onOfMode: EvaluationMode.On)
		{
		}
	}

	public class NewPaymentProcessorFeatureFlag : FeatureFlagBase
	{
		public NewPaymentProcessorFeatureFlag()
			: base(
				key: "new-payment-processor",
				name: "New Payment Processor",
				description: "Controls payment processor selection",
				onOfMode: EvaluationMode.Off)
		{
		}
	}

	public class NewEmailServiceFeatureFlag : FeatureFlagBase
	{
		public NewEmailServiceFeatureFlag()
			: base(
				key: "new-email-service",
				name: "New Email Service",
				description: "Controls email service implementation",
				onOfMode: EvaluationMode.Off)
		{
		}
	}

	public class GenericFeatureFlag : FeatureFlagBase
	{
		public GenericFeatureFlag(string key)
			: base(key: key, name: key, description: "Generic flag", onOfMode: EvaluationMode.Off)
		{
		}
	}
}