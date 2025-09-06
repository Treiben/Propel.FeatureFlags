namespace Propel.FeatureFlags.Core;

public class FlagActivationSchedule
{
	public DateTime? ScheduledEnableUtcDate { get; }
	public DateTime? ScheduledDisableUtcDate { get; }

	public FlagActivationSchedule(DateTime? scheduledEnableUtcDate, DateTime? scheduledDisableUtcDate)
	{
		ScheduledEnableUtcDate = scheduledEnableUtcDate;
		ScheduledDisableUtcDate = scheduledDisableUtcDate;
	}

	public static FlagActivationSchedule Unscheduled => new(null, null);

	// This method is used to create new flag schedules in valid state
	public static FlagActivationSchedule CreateSchedule(DateTime scheduledEnableDate, DateTime? scheduledDisableDate = null)
	{
		var utcEnableDate = scheduledEnableDate.ToUniversalTime();
		var utcDisableDate = scheduledDisableDate?.ToUniversalTime();

		if (scheduledEnableDate <= DateTime.MinValue || scheduledEnableDate >= DateTime.MaxValue)
		{
			throw new ArgumentException("Scheduled enable date must be a valid date.");
		}

		if (scheduledEnableDate <= DateTime.UtcNow)
		{
			throw new ArgumentException("Scheduled enable date must be in the future.");
		}

		if (scheduledDisableDate.HasValue && scheduledDisableDate <= scheduledEnableDate)
		{
			throw new ArgumentException("Scheduled disable date must be after the scheduled enable date.");
		}

		return new FlagActivationSchedule(utcEnableDate, utcDisableDate);
	}

	public bool HasSchedule()
	{
		return ScheduledEnableUtcDate.HasValue && ScheduledEnableUtcDate != DateTime.MinValue;
	}

	public (bool, string) IsActiveAt(DateTime evaluationTime)
	{
		// If no enable date is set, the flag is not scheduled to be enabled
		if (!ScheduledEnableUtcDate.HasValue)
		{
			return (false, "No enable date set");
		}

		var utcEvaluationTime = evaluationTime.ToUniversalTime();

		// If the current time is before the enable date, the flag is not enabled
		if (utcEvaluationTime < ScheduledEnableUtcDate.Value)
		{
			return (false, "Scheduled enable date not reached");
		}
		// If a disable date is set and the current time is after it, the flag is not enabled
		if (ScheduledDisableUtcDate.HasValue && utcEvaluationTime >= ScheduledDisableUtcDate.Value)
		{
			return (false, "Scheduled disable date passed");
		}
		// Otherwise, the flag is enabled
		return (true, "Scheduled enable date reached");
	}
}
