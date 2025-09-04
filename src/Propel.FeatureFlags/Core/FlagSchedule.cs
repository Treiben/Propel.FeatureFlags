namespace Propel.FeatureFlags.Core;

public class FlagSchedule
{
	public DateTime? ScheduledEnableDate { get; set; }
	public DateTime? ScheduledDisableDate { get; set; }

	public static FlagSchedule Unscheduled => new()
	{
		ScheduledEnableDate = null,
		ScheduledDisableDate = null
	};

	public bool IsSet()
	{
		return ScheduledEnableDate.HasValue || ScheduledDisableDate.HasValue;
	}

	public (bool, string) IsEnabled(DateTime evaluationTime)
	{
		// If no enable date is set, the flag is not scheduled to be enabled
		if (!ScheduledEnableDate.HasValue)
		{
			return (false, "No enable date set");
		}
		// If the current time is before the enable date, the flag is not enabled
		if (evaluationTime < ScheduledEnableDate.Value)
		{
			return (false, "Scheduled enable date not reached");
		}
		// If a disable date is set and the current time is after it, the flag is not enabled
		if (ScheduledDisableDate.HasValue && evaluationTime >= ScheduledDisableDate.Value)
		{
			return (false, "Scheduled disable date passed");
		}
		// Otherwise, the flag is enabled
		return (true, "Scheduled enable date reached");
	}
}
