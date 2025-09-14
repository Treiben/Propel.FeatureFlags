using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation.ApplicationScope;

namespace Propel.ClientApi.FeatureFlags;

public class NewEmailServiceFeatureFlag : TypeSafeFeatureFlag
{
	public NewEmailServiceFeatureFlag() 
		: base(key: "new-email-service",
			name: "New Email Service",
			description: "Controls whether to use the enhanced email service implementation with improved performance and features, or fall back to the legacy email service. Enables safe rollout of new email infrastructure with automatic fallback for resilience.",
			tags: new Dictionary<string, string>
			{
				{ "category", "infrastructure" },
				{ "type", "implementation-toggle" },
				{ "impact", "medium" },
				{ "team", "platform" },
				{ "rollback", "automatic" }
			},
			defaultMode: EvaluationMode.Disabled)
	{
	}

	public static IApplicationFeatureFlag Create()
	{
		return new NewEmailServiceFeatureFlag(); 
	}
}
