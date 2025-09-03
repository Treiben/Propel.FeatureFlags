namespace Propel.FeatureFlags.Core
{
	public enum FeatureFlagStatus
	{
		Disabled = 0,
		Enabled = 1,
		Scheduled = 2,
		TimeWindow = 3,
		ScheduledWithTimeWindow = 5,
		Percentage = 6,
		ScheduledWithPercentage = 8,
		TimeWindowWithPercentage = 9,
		ScheduledWithTimeWindowAndPercentage = 11,
		UserTargeted = 12,
		ScheduledWithUserTargeting = 14,
		TimeWindowWithUserTargeting = 15,
		ScheduledWithTimeWindowAndUserTargeting = 17,
		PercentageWithUserTargeting = 18,
		ScheduledWithPercentageAndUserTargeting = 20,
		TimeWindowWithPercentageAndUserTargeting = 21,
		ScheduledWithTimeWindowAndPercentageAndUserTargeting = 23
	}

	public static class FeatureFlagStatusExtensions
	{
		public static bool IsValid(this FeatureFlagStatus status)
		{
			return Enum.IsDefined(typeof(FeatureFlagStatus), status);
		}

		public static FeatureFlagStatus Increment(this FeatureFlagStatus baseStatus, FeatureFlagStatus increment)
		{
			if (baseStatus == FeatureFlagStatus.Disabled || baseStatus == FeatureFlagStatus.Enabled)
			{
				return increment;
			}
			return (FeatureFlagStatus)((int)baseStatus + (int)increment);
		}

		public static FeatureFlagStatus Decrement(this FeatureFlagStatus baseStatus, FeatureFlagStatus decrement)
		{
			if (baseStatus == FeatureFlagStatus.Disabled || baseStatus == FeatureFlagStatus.Enabled)
			{
				return baseStatus;
			}
			return (FeatureFlagStatus)((int)baseStatus - (int)decrement);
		}
	}
}
