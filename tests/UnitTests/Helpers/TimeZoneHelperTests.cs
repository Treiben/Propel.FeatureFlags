using Propel.FeatureFlags.Helpers;
using Shouldly;

namespace FeatureFlags.UnitTests.Helpers;

public class TimeZoneHelper_GetSystemTimeZones
{
	[Fact]
	public void Should_ReturnListOfTimeZoneIds()
	{
		// Act
		var result = TimeZoneHelper.GetSystemTimeZones();

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotBeEmpty();
		result.ShouldContain("UTC");
		result.ShouldAllBe(tz => !string.IsNullOrWhiteSpace(tz));
	}

	[Fact]
	public void Should_ReturnConsistentResults()
	{
		// Act
		var result1 = TimeZoneHelper.GetSystemTimeZones();
		var result2 = TimeZoneHelper.GetSystemTimeZones();

		// Assert
		result1.ShouldBe(result2);
	}
}

public class TimeZoneHelper_GetCurrentTimeAndDayInTimeZone
{
	[Fact]
	public void When_PassedUtcTime_Should_ReturnCorrectLocalTimeAndDay()
	{
		// Arrange
		var utcTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc); // Monday 5 PM UTC
		var timeZoneId = "Eastern Standard Time";

		// Act
		var (timeOfDay, dayOfWeek) = TimeZoneHelper.GetCurrentTimeAndDayInTimeZone(timeZoneId, utcTime);

		// Assert
		timeOfDay.ShouldBeOfType<TimeSpan>();
		dayOfWeek.ShouldBe(DayOfWeek.Monday);
		timeOfDay.Hours.ShouldBe(12); // EST is UTC-5, so 17:00 UTC = 12:00 EST
	}

	[Fact]
	public void When_PassedLocalTime_Should_TreatAsUtcAndConvert()
	{
		// Arrange
		var localTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Local);
		var timeZoneId = "UTC";

		// Act
		var (timeOfDay, dayOfWeek) = TimeZoneHelper.GetCurrentTimeAndDayInTimeZone(timeZoneId, localTime);

		// Assert
		timeOfDay.Hours.ShouldBe(12);
		timeOfDay.Minutes.ShouldBe(0);
		dayOfWeek.ShouldBe(DayOfWeek.Monday);
	}

	[Fact]
	public void When_PassedInvalidTimeZone_Should_ThrowTimeZoneNotFoundException()
	{
		// Arrange
		var utcTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		var invalidTimeZoneId = "Invalid/TimeZone";

		// Act & Assert
		Should.Throw<TimeZoneNotFoundException>(
			() => TimeZoneHelper.GetCurrentTimeAndDayInTimeZone(invalidTimeZoneId, utcTime));
	}

	[Fact]
	public void When_PassedUnspecifiedTime_Should_TreatAsUtcAndConvert()
	{
		// Arrange
		var unspecifiedTime = new DateTime(2024, 1, 15, 9, 30, 0, DateTimeKind.Unspecified);
		var timeZoneId = "Pacific Standard Time";

		// Act
		var (timeOfDay, dayOfWeek) = TimeZoneHelper.GetCurrentTimeAndDayInTimeZone(timeZoneId, unspecifiedTime);

		// Assert
		timeOfDay.ShouldBeOfType<TimeSpan>();
		dayOfWeek.ShouldBe(DayOfWeek.Monday);
	}

	[Fact]
	public void When_PassedInvalidTimeZone_Should_ThrowException()
	{
		// Arrange
		var utcTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		var invalidTimeZoneId = "Invalid/TimeZone";

		// Act & Assert
		Should.Throw<TimeZoneNotFoundException>(
			() => TimeZoneHelper.GetCurrentTimeAndDayInTimeZone(invalidTimeZoneId, utcTime));
	}
}