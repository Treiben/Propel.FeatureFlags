using Propel.FeatureFlags.Domain;

namespace DemoLegacyApi.FeatureFlags.ApplicationFlags
{
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
}