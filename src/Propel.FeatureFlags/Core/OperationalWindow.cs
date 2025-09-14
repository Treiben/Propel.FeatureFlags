using Propel.FeatureFlags.Helpers;

namespace Propel.FeatureFlags.Core;

public class OperationalWindow(
			TimeSpan startOn,
			TimeSpan stopOn,
			string timeZone = "UTC",
			DayOfWeek[]? daysActive = null)
{
	public TimeSpan StartOn { get; } = startOn;
	public TimeSpan StopOn { get; } = stopOn;
	public string TimeZone { get; } = timeZone ?? "UTC";
	public DayOfWeek[] DaysActive { get; } = daysActive ?? [DayOfWeek.Monday,
			DayOfWeek.Tuesday,
			DayOfWeek.Wednesday,
			DayOfWeek.Thursday,
			DayOfWeek.Friday,
			DayOfWeek.Saturday,
			DayOfWeek.Sunday];

	public static OperationalWindow AlwaysOpen => new(
		startOn: TimeSpan.Zero,
		stopOn: new TimeSpan(23, 59, 59));

	// This method is used to create a new operation window in valid state
	public static OperationalWindow CreateWindow(
											TimeSpan startTime, 
											TimeSpan endTime,
											string? timeZone = null,
											List<DayOfWeek>? allowedDays = null)
	{
		// Validate start time
		if (startTime < TimeSpan.Zero || startTime >= TimeSpan.FromDays(1))
		{
			throw new ArgumentException("Start time must be between 00:00:00 and 23:59:59.", nameof(startTime));
		}

		// Validate end time
		if (endTime < TimeSpan.Zero || endTime >= TimeSpan.FromDays(1))
		{
			throw new ArgumentException("End time must be between 00:00:00 and 23:59:59.", nameof(endTime));
		}

		// Note: We allow start time >= end time for overnight windows (e.g., 22:00 - 06:00)
		// This is intentionally different from the FlagActivationSchedule validation

		// Validate and normalize timezone
		var validatedTimeZone = ValidateAndNormalizeTimeZone(timeZone);

		// Validate allowed days
		var validatedDays = ValidateAllowedDays(allowedDays);

		return new OperationalWindow(startTime, endTime, validatedTimeZone, validatedDays);
	}

	public bool HasWindow()
	{
		return StartOn > TimeSpan.Zero && StopOn > TimeSpan.Zero;
	}

	public (bool, string) IsActiveAt(DateTime evaluationTime, string? contextTimeZone = null)
	{
		var effectiveTimeZone = contextTimeZone ?? TimeZone;
		
		try
		{
			var (currentTime, currentDay) = TimeZoneHelper.GetCurrentTimeAndDayInTimeZone(effectiveTimeZone, evaluationTime);

			// Check if current day is in allowed days
			if (!IsAllowedDay(currentDay))
			{
				return (false, "Outside allowed days");
			}

			// Check if current time is within window
			if (!IsWithinTimeRange(currentTime))
			{
				return (false, "Outside time window");
			}

			return (true, "Within time window");
		}
		catch (TimeZoneNotFoundException)
		{
			return (false, $"Invalid timezone: {effectiveTimeZone}");
		}
		catch (InvalidTimeZoneException)
		{
			return (false, $"Invalid timezone: {effectiveTimeZone}");
		}
	}

	private bool IsAllowedDay(DayOfWeek dayOfWeek)
	{
		return DaysActive.Contains(dayOfWeek);
	}

	private bool IsWithinTimeRange(TimeSpan currentTime)
	{
		if (StartOn <= StopOn)
		{
			// Same day window (e.g., 9:00 - 17:00)
			return currentTime >= StartOn && currentTime <= StopOn;
		}
		else
		{
			// Overnight window (e.g., 22:00 - 06:00)
			return currentTime >= StartOn || currentTime <= StopOn;
		}
	}

	private static string ValidateAndNormalizeTimeZone(string? timeZone)
	{
		if (string.IsNullOrWhiteSpace(timeZone))
		{
			return "UTC";
		}

		try
		{
			// Validate that the timezone exists
			TimeZoneInfo.FindSystemTimeZoneById(timeZone);
			return timeZone!;
		}
		catch (TimeZoneNotFoundException)
		{
			throw new ArgumentException($"Invalid timezone identifier: {timeZone}", nameof(timeZone));
		}
		catch (InvalidTimeZoneException)
		{
			throw new ArgumentException($"Invalid timezone identifier: {timeZone}", nameof(timeZone));
		}
	}

	private static DayOfWeek[] ValidateAllowedDays(List<DayOfWeek>? allowedDays)
	{
		if (allowedDays == null || allowedDays.Count == 0)
		{
			// Default to all days if none specified
			return [DayOfWeek.Monday, 
				DayOfWeek.Tuesday, 
				DayOfWeek.Wednesday, 
				DayOfWeek.Thursday,
				DayOfWeek.Friday, 
				DayOfWeek.Saturday,
				DayOfWeek.Sunday];
		}

		// Remove duplicates and validate enum values
		var distinctDays = allowedDays.Distinct().ToArray();
		foreach (var day in distinctDays)
		{
			if (!Enum.IsDefined(typeof(DayOfWeek), day))
			{
				throw new ArgumentException($"Invalid day of week: {day}", nameof(allowedDays));
			}
		}

		return distinctDays;
	}

	public static bool operator ==(OperationalWindow? left, OperationalWindow? right)
	{
		if (ReferenceEquals(left, right))
			return true;

		if (left is null || right is null)
			return false;

		return left.StartOn == right.StartOn &&
			   left.StopOn == right.StopOn &&
			   string.Equals(left.TimeZone, right.TimeZone, StringComparison.OrdinalIgnoreCase) &&
			   left.DaysActive.OrderBy(d => d).SequenceEqual(right.DaysActive.OrderBy(d => d));
	}

	public static bool operator !=(OperationalWindow? left, OperationalWindow? right)
	{
		return !(left == right);
	}

	public override bool Equals(object? obj)
	{
		return obj is OperationalWindow other && this == other;
	}

	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(StartOn);
		hash.Add(StopOn);
		hash.Add(TimeZone, StringComparer.OrdinalIgnoreCase);

		// Add sorted days to ensure consistent hash regardless of order
		foreach (var day in DaysActive.OrderBy(d => d))
		{
			hash.Add(day);
		}

		return hash.ToHashCode();
	}
}