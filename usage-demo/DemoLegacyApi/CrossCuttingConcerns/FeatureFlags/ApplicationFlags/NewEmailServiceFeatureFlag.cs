using Propel.FeatureFlags.Domain;

namespace DemoLegacyApi.FeatureFlags.ApplicationFlags
{
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
}