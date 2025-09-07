namespace Propel.FeatureFlags.Core;

public class FlagActivationSchedule
{
	public DateTime ScheduledEnableDate { get; }
	public DateTime ScheduledDisableDate { get; }

	public FlagActivationSchedule(DateTime scheduledEnableDate, DateTime? scheduledDisableDate = null)
	{
		var scheduledEnableUtcDate = scheduledEnableDate.ToUniversalTime();
		var scheduledDisableUtcDate = (scheduledDisableDate ?? DateTime.MaxValue).ToUniversalTime();

		if (scheduledDisableUtcDate <= scheduledEnableUtcDate)
		{
			throw new ArgumentException("Scheduled disable date must be after the scheduled enable date.");
		}

		ScheduledEnableDate = scheduledEnableUtcDate;
		ScheduledDisableDate = scheduledDisableUtcDate;
	}

	public static FlagActivationSchedule Unscheduled => new(DateTime.MinValue, DateTime.MaxValue);

	// This method is used to create new flag schedules in valid state
	public static FlagActivationSchedule CreateSchedule(DateTime scheduledEnableDate, DateTime? scheduledDisableDate = null)
	{

		if (scheduledEnableDate <= DateTime.MinValue)
		{
			throw new ArgumentException("Scheduled enable date must be a valid date.");
		}

		if (scheduledEnableDate.ToUniversalTime() <= DateTime.UtcNow)
		{
			throw new ArgumentException("Scheduled enable date must be in the future.");
		}

		return new FlagActivationSchedule(scheduledEnableDate, scheduledDisableDate);
	}

	public bool HasSchedule()
	{
		var hasStartSchedule = ScheduledEnableDate > DateTime.MinValue.ToUniversalTime();
		var hasNoStartSchedule = ScheduledEnableDate == DateTime.MinValue.ToUniversalTime();

		var hasEndSchedule = ScheduledDisableDate < DateTime.MaxValue.ToUniversalTime();
		var hasNoEndSchedule = ScheduledDisableDate == DateTime.MaxValue.ToUniversalTime();

		return (hasStartSchedule && (hasEndSchedule || hasNoEndSchedule))
			|| (hasEndSchedule && (hasStartSchedule || hasNoStartSchedule));
	}

	public (bool, string) IsActiveAt(DateTime evaluationTime)
	{
		// If no enable date is set, the flag is not scheduled to be enabled
		if (!HasSchedule())
		{
			return (false, "No active schedule set");
		}

		var utcEvaluationTime = evaluationTime.ToUniversalTime();

		// If the current time is before the enable date, the flag is not enabled
		if (utcEvaluationTime < ScheduledEnableDate)
		{
			return (false, "Scheduled enable date not reached");
		}
		// If a disable date is set and the current time is after it, the flag is not enabled
		if (utcEvaluationTime >= ScheduledDisableDate)
		{
			return (false, "Scheduled disable date passed");
		}
		// Otherwise, the flag is enabled
		return (true, "Scheduled enable date reached");
	}
}
