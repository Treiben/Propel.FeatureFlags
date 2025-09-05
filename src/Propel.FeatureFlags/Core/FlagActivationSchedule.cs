using System.Runtime.CompilerServices;

namespace Propel.FeatureFlags.Core;

public class FlagActivationSchedule
{
	public DateTime? ScheduledEnableDate { get; }
	public DateTime? ScheduledDisableDate { get; }

	// Private constructor to prevent direct instantiation
	internal FlagActivationSchedule(DateTime? scheduledEnableDate = null, DateTime? scheduledDisableDate = null)
	{
		ScheduledEnableDate = scheduledEnableDate;
		ScheduledDisableDate = scheduledDisableDate;
	}

	public static FlagActivationSchedule Unscheduled => new();

	// This method is used to load schedules from persistent storage because it skips validation
	public static FlagActivationSchedule LoadSchedule(DateTime? scheduledEnableDate, DateTime? scheduledDisableDate)
	{
		return new FlagActivationSchedule(scheduledEnableDate, scheduledDisableDate);
	}

	// This method is used to create new flag schedules in valid state
	public static FlagActivationSchedule CreateSchedule(DateTime scheduledEnableDate, DateTime? scheduledDisableDate = null)
	{
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

		return new FlagActivationSchedule(scheduledEnableDate, scheduledDisableDate);
	}

	public bool HasSchedule()
	{
		return ScheduledEnableDate.HasValue && ScheduledEnableDate != DateTime.MinValue;
	}

	public (bool, string) IsActiveAt(DateTime evaluationTime)
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
