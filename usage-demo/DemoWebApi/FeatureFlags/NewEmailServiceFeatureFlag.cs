using Propel.FeatureFlags.Domain;

namespace DemoWebApi.FeatureFlags;

public class NewEmailServiceFeatureFlag : FeatureFlagBase
{
	public NewEmailServiceFeatureFlag() 
		: base(key: "new-email-service",
			name: "New Email Service",
			description: "Controls whether to use the enhanced email service implementation with improved performance and features, or fall back to the legacy email service. Enables safe rollout of new email infrastructure with automatic fallback for resilience.",
			onOfMode: EvaluationMode.Off)
	{
	}
}
