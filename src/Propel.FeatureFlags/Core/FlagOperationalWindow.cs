using System;

namespace Propel.FeatureFlags.Core;

public class FlagOperationalWindow(
			TimeSpan windowStartTime,
			TimeSpan windowEndTime,
			string timeZone = "UTC",
			DayOfWeek[]? windowDays = null)
{
	public TimeSpan WindowStartTime { get; } = windowStartTime;
	public TimeSpan WindowEndTime { get; } = windowEndTime;
	public string TimeZone { get; } = timeZone ?? "UTC";
	public DayOfWeek[] WindowDays { get; } = windowDays ?? [DayOfWeek.Monday,
			DayOfWeek.Tuesday,
			DayOfWeek.Wednesday,
			DayOfWeek.Thursday,
			DayOfWeek.Friday,
			DayOfWeek.Saturday,
			DayOfWeek.Sunday];

	public static FlagOperationalWindow AlwaysOpen => new(
		windowStartTime: TimeSpan.Zero,
		windowEndTime: new TimeSpan(23, 59, 59));

	// This method is used to create a new operation window in valid state
	public static FlagOperationalWindow CreateWindow(
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

		return new FlagOperationalWindow(startTime, endTime, validatedTimeZone, validatedDays);
	}

	public bool HasWindow()
	{
		return WindowStartTime > TimeSpan.Zero && WindowEndTime > TimeSpan.Zero;
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
		return WindowDays.Contains(dayOfWeek);
	}

	private bool IsWithinTimeRange(TimeSpan currentTime)
	{
		if (WindowStartTime <= WindowEndTime)
		{
			// Same day window (e.g., 9:00 - 17:00)
			return currentTime >= WindowStartTime && currentTime <= WindowEndTime;
		}
		else
		{
			// Overnight window (e.g., 22:00 - 06:00)
			return currentTime >= WindowStartTime || currentTime <= WindowEndTime;
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

	public static bool operator ==(FlagOperationalWindow? left, FlagOperationalWindow? right)
	{
		if (ReferenceEquals(left, right))
			return true;

		if (left is null || right is null)
			return false;

		return left.WindowStartTime == right.WindowStartTime &&
			   left.WindowEndTime == right.WindowEndTime &&
			   string.Equals(left.TimeZone, right.TimeZone, StringComparison.OrdinalIgnoreCase) &&
			   left.WindowDays.OrderBy(d => d).SequenceEqual(right.WindowDays.OrderBy(d => d));
	}

	public static bool operator !=(FlagOperationalWindow? left, FlagOperationalWindow? right)
	{
		return !(left == right);
	}

	public override bool Equals(object? obj)
	{
		return obj is FlagOperationalWindow other && this == other;
	}

	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(WindowStartTime);
		hash.Add(WindowEndTime);
		hash.Add(TimeZone, StringComparer.OrdinalIgnoreCase);

		// Add sorted days to ensure consistent hash regardless of order
		foreach (var day in WindowDays.OrderBy(d => d))
		{
			hash.Add(day);
		}

		return hash.ToHashCode();
	}
}

public static class TimeZoneHelper
{
	public static List<string> GetSystemTimeZones()
	{
		var timeZones = new List<string>();
		foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
		{
			timeZones.Add(tz.Id);
		}
		return timeZones;
	}

	public static (TimeSpan, DayOfWeek) GetCurrentTimeAndDayInTimeZone(string timeZoneId, DateTime evaluationTime)
	{
		var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
		
		// Ensure we're working with UTC time
		if (evaluationTime.Kind != DateTimeKind.Utc)
		{
			evaluationTime = DateTime.SpecifyKind(evaluationTime, DateTimeKind.Utc);
		}
		
		var localTime = TimeZoneInfo.ConvertTimeFromUtc(evaluationTime, timeZoneInfo);
		var currentTime = localTime.TimeOfDay;
		var currentDay = localTime.DayOfWeek;

		return (currentTime, currentDay);
	}
}