namespace Propel.FeatureFlags.Core
{
	public enum FeatureFlagStatus
	{
		Disabled = 0,
		Enabled = 1,
		Scheduled = 2,
		TimeWindow = 3,
		UserTargeted = 4,
		Percentage = 5
	}
}
