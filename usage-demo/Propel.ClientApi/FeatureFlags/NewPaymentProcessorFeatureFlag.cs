using Propel.FeatureFlags.Core;

namespace Propel.ClientApi.FeatureFlags;

public class NewPaymentProcessorFeatureFlag : TypeSafeFeatureFlag
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
