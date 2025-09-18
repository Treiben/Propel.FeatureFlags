using Propel.FeatureFlags.Services.ApplicationScope;

namespace Propel.ClientApi.FeatureFlags;

// The flag created with default settings if it does not already exist in the database
// in disabled state (Recommended).

// Note: It is often safer to default to disabled so when the feature is deployed it can be enabled
// on approved release schedule from management tools rather than immeditately,
public class NewPaymentProcessorFeatureFlag : RegisteredFeatureFlag
{
	public NewPaymentProcessorFeatureFlag()
		: base(key: "new-payment-processor",
			name: "New Payment Processor",
			description: "Controls whether to use the enhanced payment processing implementation with improved performance and features, or fall back to the legacy processor. Enables gradual rollout with automatic fallback for resilience and risk mitigation during payment processing.",
			tags: new Dictionary<string, string>
			{
				{ "category", "payment" },
				{ "type", "implementation-toggle" },
				{ "impact", "high" },
				{ "team", "payments" },
				{ "rollback", "automatic" },
				{ "critical", "true" }
			})
	{
	}
}
