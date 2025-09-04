namespace Propel.FeatureFlags.Core;

public class FlagTimeWindow
{
	public TimeSpan? WindowStartTime { get; set; }
	public TimeSpan? WindowEndTime { get; set; }
	public string? TimeZone { get; set; }
	public List<DayOfWeek>? WindowDays { get; set; }

	public static FlagTimeWindow AlwaysOpen => new()
	{
		WindowStartTime = null,
		WindowEndTime = null,
		TimeZone = "UTC",
		WindowDays =
		[
			DayOfWeek.Monday,
			DayOfWeek.Tuesday,
			DayOfWeek.Wednesday,
			DayOfWeek.Thursday,
			DayOfWeek.Friday,
			DayOfWeek.Saturday,
			DayOfWeek.Sunday
		]
	};
	public bool IsSet()
	{
		return WindowStartTime.HasValue && WindowEndTime.HasValue;
	}

	public bool InWindowDays(DayOfWeek dayOfWeek)
	{
		if (!IsSet())
		{
			throw new InvalidOperationException("Time window is not properly configured.");
		}

		// Check if current day is in allowed days
		if (WindowDays?.Any() == true && !WindowDays.Contains(dayOfWeek))
		{
			return false;
		}
		return true;
	}

	public bool InWindowTime(TimeSpan time)
	{
		if (!IsSet())
		{
			throw new InvalidOperationException("Time window is not properly configured.");
		}

		bool inWindow;
		if (WindowStartTime!.Value <= WindowEndTime!.Value)
		{
			// Same day window (e.g., 9:00 - 17:00)
			inWindow = time >= WindowStartTime.Value && time <= WindowEndTime.Value;
		}
		else
		{
			// Overnight window (e.g., 22:00 - 06:00)
			inWindow = time >= WindowStartTime.Value || time <= WindowEndTime.Value.Add(TimeSpan.FromHours(24));
		}
		return inWindow;
	}

	public string GetTimeZone()
	{
		return string.IsNullOrEmpty(TimeZone) ? "UTC" : TimeZone;
	}
}

public class TimeZoneHelper
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
		if (evaluationTime.Kind != DateTimeKind.Utc)
		{
			evaluationTime = DateTime.SpecifyKind(evaluationTime, DateTimeKind.Unspecified);
		}
		var localTime = TimeZoneInfo.ConvertTimeFromUtc(evaluationTime, timeZoneInfo);
		var currentTime = localTime.TimeOfDay;
		var currentDay = localTime.DayOfWeek;

		return (currentTime, currentDay);
	}
}
