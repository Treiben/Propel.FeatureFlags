using Propel.FeatureFlags.Helpers;

namespace Propel.FeatureFlags.Domain;

public class ActivationSchedule
{
	public DateTime EnableOn { get; }
	public DateTime DisableOn { get; }

	public ActivationSchedule(DateTime enableOn, DateTime disableOn)
	{
		var enableOnUtc = DateTimeHelpers.NormalizeToUtc(enableOn, utcReplacementDt: DateTime.MinValue);
		var disableOnUtc = DateTimeHelpers.NormalizeToUtc(disableOn, utcReplacementDt: DateTime.MaxValue);

		if (disableOnUtc <= enableOnUtc)
		{
			throw new ArgumentException("Scheduled disable date must be after the scheduled enable date.");
		}

		EnableOn = enableOnUtc;
		DisableOn = disableOnUtc;
	}

	public static ActivationSchedule Unscheduled => new(DateTime.MinValue, DateTime.MaxValue);

	// This method is used to create new flag schedules in valid state
	public static ActivationSchedule CreateSchedule(DateTime? enableOn, DateTime? disableOn = null)
	{
		var enableOnUtc = DateTimeHelpers.NormalizeToUtc(enableOn, utcReplacementDt: DateTime.MinValue);
		if (enableOnUtc <= DateTime.UtcNow)
		{
			throw new ArgumentException("Scheduled enable date must be in the future.");
		}

		var disableOnUtc = DateTimeHelpers.NormalizeToUtc(disableOn, utcReplacementDt: DateTime.MaxValue);

		return new ActivationSchedule(enableOnUtc, disableOnUtc);
	}

	public bool HasSchedule()
	{
		return EnableOn > DateTime.MinValue.ToUniversalTime() || DisableOn < DateTime.MaxValue.ToUniversalTime();
	}

	public (bool, string) IsActiveAt(DateTime evaluationTime)
	{
		// If no enable date is set, the flag is not scheduled to be enabled
		if (!HasSchedule())
		{
			return (false, "No active schedule set");
		}

		var utcEvaluationTime = DateTimeHelpers.NormalizeToUtc(evaluationTime, utcReplacementDt: DateTime.UtcNow);

		// If the current time is before the enable date, the flag is not enabled
		if (utcEvaluationTime < EnableOn)
		{
			return (false, "Scheduled enable date not reached");
		}
		// If a disable date is set and the current time is after it, the flag is not enabled
		if (utcEvaluationTime >= DisableOn)
		{
			return (false, "Scheduled disable date passed");
		}
		// Otherwise, the flag is enabled
		return (true, "Scheduled enable date reached");
	}

	public static bool operator ==(ActivationSchedule? left, ActivationSchedule? right)
	{
		if (left is null && right is null) 
			return true;
		if (left is null || right is null) 
			return false;

		return left.EnableOn == right.EnableOn
			&& left.DisableOn == right.DisableOn;
	}

	public static bool operator !=(ActivationSchedule? left, ActivationSchedule? right)
	{
		return !(left == right);
	}

	public override bool Equals(object obj)
	{
		return obj is ActivationSchedule other && this == other;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(EnableOn, DisableOn);
	}
}
