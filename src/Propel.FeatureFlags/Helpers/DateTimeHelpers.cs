namespace Propel.FeatureFlags.Helpers;

public static class DateTimeHelpers
{
	public static DateTime NormalizeToUtc(DateTime? dateTime, DateTime utcReplacementDt)
	{
		if (!dateTime.HasValue) 
			return utcReplacementDt;

		if (dateTime.Value == DateTime.MinValue)
			return DateTime.MinValue.ToUniversalTime();

		if (dateTime.Value == DateTime.MinValue.ToUniversalTime())
			return dateTime.Value;

		if (dateTime.Value == DateTime.MaxValue)
			return DateTime.MaxValue.ToUniversalTime();

		if (dateTime.Value == DateTime.MaxValue.ToUniversalTime())
			return dateTime.Value;

		if (dateTime.Value.Kind == DateTimeKind.Utc)
		{
			return dateTime.Value;
		}

		return dateTime.Value.ToUniversalTime();
	}

	public static DateTime NormalizeToUtc(DateTimeOffset? dateTimeOffset, DateTime utcReplacmentDt)
	{
		if (!dateTimeOffset.HasValue)
			return utcReplacmentDt;

		if (dateTimeOffset.Value == DateTimeOffset.MinValue)
			return DateTime.MinValue.ToUniversalTime();

		if (dateTimeOffset.Value == DateTimeOffset.MaxValue)
			return DateTime.MaxValue.ToUniversalTime();

		var dateTime = dateTimeOffset!.Value.DateTime;
		if (dateTimeOffset!.Value.Offset == TimeSpan.Zero)
		{
			dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
			return dateTime;
		}

		dateTime = dateTimeOffset.Value.DateTime.ToUniversalTime();
		dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
		return dateTime;
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
