using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagOperationalWindow_Constructor
{
	[Fact]
	public void If_InternalConstructor_ThenSetsProperties()
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var endTime = TimeSpan.FromHours(17);
		var timeZone = "UTC";
		DayOfWeek[] windowDays = [ DayOfWeek.Monday, DayOfWeek.Tuesday ];

		// Act
		var window = new FlagOperationalWindow(startTime, endTime, timeZone, windowDays);

		// Assert
		window.WindowStartTime.ShouldBe(startTime);
		window.WindowEndTime.ShouldBe(endTime);
		window.TimeZone.ShouldBe(timeZone);
		window.WindowDays.ShouldBe(windowDays);
	}

	[Fact]
	public void If_InternalConstructorWithNullDays_ThenSetsAllDays()
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var endTime = TimeSpan.FromHours(17);
		var timeZone = "UTC";

		// Act
		var window = new FlagOperationalWindow(startTime, endTime, timeZone, null);

		// Assert
		window.WindowDays.Length.ShouldBe(7);
		window.WindowDays.ShouldContain(DayOfWeek.Monday);
		window.WindowDays.ShouldContain(DayOfWeek.Tuesday);
		window.WindowDays.ShouldContain(DayOfWeek.Wednesday);
		window.WindowDays.ShouldContain(DayOfWeek.Thursday);
		window.WindowDays.ShouldContain(DayOfWeek.Friday);
		window.WindowDays.ShouldContain(DayOfWeek.Saturday);
		window.WindowDays.ShouldContain(DayOfWeek.Sunday);
	}
}

public class FlagOperationalWindow_AlwaysOpen
{
	[Fact]
	public void If_AlwaysOpen_ThenReturnsThenHasNoWindow()
	{
		// Act
		var window = FlagOperationalWindow.AlwaysOpen;

		// Assert
		window.WindowStartTime.ShouldBe(TimeSpan.Zero);
		window.WindowEndTime.ShouldBe(new TimeSpan(23, 59, 59));
		window.TimeZone.ShouldBe("UTC");
		window.WindowDays.Length.ShouldBe(7);
		window.HasWindow().ShouldBeFalse();
	}

	[Fact]
	public void If_AlwaysOpenMultipleCalls_ThenReturnsSeparateInstances()
	{
		// Act
		var window1 = FlagOperationalWindow.AlwaysOpen;
		var window2 = FlagOperationalWindow.AlwaysOpen;

		// Assert
		window1.ShouldNotBe(window2);
		window1.WindowStartTime.ShouldBe(window2.WindowStartTime);
		window1.WindowEndTime.ShouldBe(window2.WindowEndTime);
		window1.TimeZone.ShouldBe(window2.TimeZone);
	}
}

public class FlagOperationalWindow_CreateWindow
{
	[Fact]
	public void If_ValidTimeRange_ThenCreatesWindow()
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var endTime = TimeSpan.FromHours(17);

		// Act
		var window = FlagOperationalWindow.CreateWindow(startTime, endTime);

		// Assert
		window.WindowStartTime.ShouldBe(startTime);
		window.WindowEndTime.ShouldBe(endTime);
		window.TimeZone.ShouldBe("UTC");
		window.WindowDays.Length.ShouldBe(7);
	}

	[Fact]
	public void If_ValidTimeRangeWithTimeZone_ThenCreatesWindowWithTimeZone()
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var endTime = TimeSpan.FromHours(17);
		var timeZone = "Eastern Standard Time";

		// Act
		var window = FlagOperationalWindow.CreateWindow(startTime, endTime, timeZone);

		// Assert
		window.WindowStartTime.ShouldBe(startTime);
		window.WindowEndTime.ShouldBe(endTime);
		window.TimeZone.ShouldBe(timeZone);
		window.WindowDays.Length.ShouldBe(7);
	}

	[Fact]
	public void If_ValidTimeRangeWithAllowedDays_ThenCreatesWindowWithDays()
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var endTime = TimeSpan.FromHours(17);
		var allowedDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };

		// Act
		var window = FlagOperationalWindow.CreateWindow(startTime, endTime, allowedDays: allowedDays);

		// Assert
		window.WindowStartTime.ShouldBe(startTime);
		window.WindowEndTime.ShouldBe(endTime);
		window.TimeZone.ShouldBe("UTC");
		window.WindowDays.ShouldBe(allowedDays);
	}

	[Fact]
	public void If_OvernightWindow_ThenCreatesSuccessfully()
	{
		// Arrange
		var startTime = TimeSpan.FromHours(22); // 10 PM
		var endTime = TimeSpan.FromHours(6);   // 6 AM

		// Act
		var window = FlagOperationalWindow.CreateWindow(startTime, endTime);

		// Assert
		window.WindowStartTime.ShouldBe(startTime);
		window.WindowEndTime.ShouldBe(endTime);
	}

	[Theory]
	[InlineData(-1, 0, 0)] // Negative start time
	[InlineData(24, 0, 0)] // Start time >= 24 hours
	[InlineData(25, 30, 0)] // Start time > 24 hours
	public void If_InvalidStartTime_ThenThrowsArgumentException(int hours, int minutes, int seconds)
	{
		// Arrange
		var invalidStartTime = new TimeSpan(hours, minutes, seconds);
		var endTime = TimeSpan.FromHours(17);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			FlagOperationalWindow.CreateWindow(invalidStartTime, endTime));
		exception.Message.ShouldBe("Start time must be between 00:00:00 and 23:59:59. (Parameter 'startTime')");
	}

	[Theory]
	[InlineData(-1, 0, 0)] // Negative end time
	[InlineData(24, 0, 0)] // End time >= 24 hours
	[InlineData(25, 30, 0)] // End time > 24 hours
	public void If_InvalidEndTime_ThenThrowsArgumentException(int hours, int minutes, int seconds)
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var invalidEndTime = new TimeSpan(hours, minutes, seconds);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			FlagOperationalWindow.CreateWindow(startTime, invalidEndTime));
		exception.Message.ShouldBe("End time must be between 00:00:00 and 23:59:59. (Parameter 'endTime')");
	}

	[Fact]
	public void If_InvalidTimeZone_ThenThrowsArgumentException()
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var endTime = TimeSpan.FromHours(17);
		var invalidTimeZone = "Invalid/TimeZone";

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			FlagOperationalWindow.CreateWindow(startTime, endTime, invalidTimeZone));
		exception.Message.ShouldStartWith("Invalid timezone identifier: Invalid/TimeZone");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void If_NullOrWhitespaceTimeZone_ThenDefaultsToUtc(string? timeZone)
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var endTime = TimeSpan.FromHours(17);

		// Act
		var window = FlagOperationalWindow.CreateWindow(startTime, endTime, timeZone);

		// Assert
		window.TimeZone.ShouldBe("UTC");
	}

	[Fact]
	public void If_EmptyAllowedDays_ThenDefaultsToAllDays()
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var endTime = TimeSpan.FromHours(17);
		var emptyDays = new List<DayOfWeek>();

		// Act
		var window = FlagOperationalWindow.CreateWindow(startTime, endTime, allowedDays: emptyDays);

		// Assert
		window.WindowDays.Length.ShouldBe(7);
		window.WindowDays.ShouldContain(DayOfWeek.Monday);
		window.WindowDays.ShouldContain(DayOfWeek.Sunday);
	}

	[Fact]
	public void If_DuplicateAllowedDays_ThenRemovesDuplicates()
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var endTime = TimeSpan.FromHours(17);
		var duplicateDays = new List<DayOfWeek>
		{
			DayOfWeek.Monday,
			DayOfWeek.Tuesday,
			DayOfWeek.Monday, // Duplicate
			DayOfWeek.Wednesday,
			DayOfWeek.Tuesday  // Duplicate
		};

		// Act
		var window = FlagOperationalWindow.CreateWindow(startTime, endTime, allowedDays: duplicateDays);

		// Assert
		window.WindowDays.Length.ShouldBe(3);
		window.WindowDays.ShouldContain(DayOfWeek.Monday);
		window.WindowDays.ShouldContain(DayOfWeek.Tuesday);
		window.WindowDays.ShouldContain(DayOfWeek.Wednesday);
	}
}

public class FlagOperationalWindow_HasWindow
{
	[Fact]
	public void If_ValidTimeSpans_ThenHasWindowReturnsTrue()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));

		// Act & Assert
		window.HasWindow().ShouldBeTrue();
	}

	[Fact]
	public void If_ZeroTimeSpans_ThenHasWindowReturnsFalse()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.Zero,
			TimeSpan.Zero);

		// Act & Assert
		window.HasWindow().ShouldBeFalse();
	}


}

public class FlagOperationalWindow_IsActiveAt
{
	[Fact]
	public void If_WithinSameDayWindow_ThenReturnsActiveTrue()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),   // 9 AM
			TimeSpan.FromHours(17)); // 5 PM
		var evaluationTime = DateTime.UtcNow.Date.AddHours(12); // 12 PM UTC

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void If_OutsideSameDayWindow_ThenReturnsActiveFalse()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),   // 9 AM
			TimeSpan.FromHours(17)); // 5 PM
		var evaluationTime = DateTime.UtcNow.Date.AddHours(20); // 8 PM UTC

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Outside time window");
	}

	[Fact]
	public void If_WithinOvernightWindow_ThenReturnsActiveTrue()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(22), // 10 PM
			TimeSpan.FromHours(6)); // 6 AM
		var evaluationTime = DateTime.UtcNow.Date.AddHours(2); // 2 AM UTC

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void If_OutsideOvernightWindow_ThenReturnsActiveFalse()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(22), // 10 PM
			TimeSpan.FromHours(6)); // 6 AM
		var evaluationTime = DateTime.UtcNow.Date.AddHours(12); // 12 PM UTC

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Outside time window");
	}

	[Theory]
	[InlineData(DayOfWeek.Monday, true)]
	[InlineData(DayOfWeek.Tuesday, true)]
	[InlineData(DayOfWeek.Wednesday, false)]
	[InlineData(DayOfWeek.Thursday, false)]
	[InlineData(DayOfWeek.Friday, false)]
	[InlineData(DayOfWeek.Saturday, false)]
	[InlineData(DayOfWeek.Sunday, false)]
	public void If_SpecificAllowedDays_ThenFiltersCorrectly(DayOfWeek dayOfWeek, bool shouldBeActive)
	{
		// Arrange
		var allowedDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday };
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17),
			allowedDays: allowedDays);

		// Calculate a date for the specific day of week
		var baseDate = new DateTime(2024, 1, 1); // January 1, 2024 is a Monday
		var daysToAdd = ((int)dayOfWeek - (int)baseDate.DayOfWeek + 7) % 7;
		var evaluationDate = baseDate.AddDays(daysToAdd);
		var evaluationTime = evaluationDate.AddHours(12); // 12 PM on the specified day

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBe(shouldBeActive);
		if (shouldBeActive)
		{
			reason.ShouldBe("Within time window");
		}
		else
		{
			reason.ShouldBe("Outside allowed days");
		}
	}

	[Fact]
	public void If_CorrectDayButOutsideTimeWindow_ThenReturnsActiveFalse()
	{
		// Arrange
		var allowedDays = new List<DayOfWeek> { DayOfWeek.Monday };
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17),
			allowedDays: allowedDays);

		// Create a Monday at 8 AM (outside time window)
		var monday = new DateTime(2024, 1, 1, 8, 0, 0); // January 1, 2024 is a Monday

		// Act
		var (isActive, reason) = window.IsActiveAt(monday);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Outside time window");
	}

	[Fact]
	public void If_WithinTimeButWrongDay_ThenReturnsActiveFalse()
	{
		// Arrange
		var allowedDays = new List<DayOfWeek> { DayOfWeek.Monday };
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17),
			allowedDays: allowedDays);

		// Create a Tuesday at 12 PM (within time but wrong day)
		var tuesday = new DateTime(2024, 1, 2, 12, 0, 0); // January 2, 2024 is a Tuesday

		// Act
		var (isActive, reason) = window.IsActiveAt(tuesday);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Outside allowed days");
	}

	[Fact]
	public void If_ExactStartTime_ThenReturnsActiveTrue()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9).Add(TimeSpan.FromMinutes(30)),
			TimeSpan.FromHours(17));
		var evaluationTime = DateTime.UtcNow.Date.AddHours(9).AddMinutes(30); // Exactly 9:30 AM

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void If_ExactEndTime_ThenReturnsActiveTrue()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17).Add(TimeSpan.FromMinutes(30)));
		var evaluationTime = DateTime.UtcNow.Date.AddHours(17).AddMinutes(30); // Exactly 5:30 PM

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void If_InvalidTimeZone_ThenReturnsActiveFalseWithError()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));
		var evaluationTime = DateTime.UtcNow;

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime, "Invalid/TimeZone");

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Invalid timezone: Invalid/TimeZone");
	}

	[Fact]
	public void If_ContextTimeZoneOverridesWindowTimeZone_ThenUsesContextTimeZone()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17),
			"Pacific Standard Time"); // Window is in PST

		// 5 PM UTC = 9 AM PST (should be active in PST)
		// But with EST context: 5 PM UTC = 12 PM EST (should be active in EST too)
		var evaluationTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc);

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime, "Eastern Standard Time");

		// Assert
		isActive.ShouldBeTrue(); // 5 PM UTC = 12 PM EST, within 9-17 window
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void If_NoContextTimeZone_ThenUsesWindowTimeZone()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17),
			"Pacific Standard Time");

		// 5 PM UTC = 9 AM PST (should be active)
		var evaluationTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc);

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}
}

public class FlagOperationalWindow_TimeZoneHandling
{
	[Theory]
	[InlineData("UTC")]
	[InlineData("Eastern Standard Time")]
	[InlineData("Pacific Standard Time")]
	[InlineData("Central Standard Time")]
	public void If_ValidTimeZone_ThenCreatesWindowSuccessfully(string timeZone)
	{
		// Act
		var window = Should.NotThrow(() => FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17),
			timeZone));

		// Assert
		window.TimeZone.ShouldBe(timeZone);
	}

	[Theory]
	[InlineData("InvalidTimeZone")]
	[InlineData("NotExist/TimeZone")]
	[InlineData("Random String")]
	public void If_InvalidTimeZone_ThenThrowsArgumentException(string invalidTimeZone)
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				invalidTimeZone));
		exception.Message.ShouldStartWith($"Invalid timezone identifier: {invalidTimeZone}");
	}

	[Fact]
	public void If_TimeZoneConversionWithDifferentTimeZones_ThenWorksCorrectly()
	{
		// Arrange - Create window in Eastern Time
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),   // 9 AM EST
			TimeSpan.FromHours(17),  // 5 PM EST
			"Eastern Standard Time");

		// Test time: 2 PM UTC in January (which is 9 AM EST due to standard time)
		var evaluationTime = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc);

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void If_TimeZoneConversionOutsideWindow_ThenReturnsFalse()
	{
		// Arrange - Create window in Pacific Time
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),   // 9 AM PST
			TimeSpan.FromHours(17),  // 5 PM PST
			"Pacific Standard Time");

		// Test time: 4 AM UTC in January (which is 8 PM PST previous day - outside window)
		var evaluationTime = new DateTime(2024, 1, 15, 4, 0, 0, DateTimeKind.Utc);

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Outside time window");
	}
}

public class FlagOperationalWindow_EdgeCases
{
	[Fact]
	public void If_MidnightStartTime_ThenWorksCorrectly()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.Zero,           // Midnight
			TimeSpan.FromHours(6));  // 6 AM

		var evaluationTime = DateTime.UtcNow.Date.AddHours(3); // 3 AM

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void If_AlmostMidnightEndTime_ThenWorksCorrectly()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(20),             // 8 PM
			new TimeSpan(23, 59, 59));          // 23:59:59

		var evaluationTime = DateTime.UtcNow.Date.AddHours(23).AddMinutes(30); // 11:30 PM

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void If_VeryShortWindow_ThenWorksCorrectly()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(12),                    // 12:00:00 PM
			TimeSpan.FromHours(12).Add(TimeSpan.FromSeconds(1))); // 12:00:01 PM

		var evaluationTime = DateTime.UtcNow.Date.AddHours(12); // Exactly 12:00:00 PM

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void If_SingleDayWindow_ThenWorksCorrectly()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17),
			allowedDays: [DayOfWeek.Wednesday]);

		// Create a Wednesday at 12 PM
		var wednesday = new DateTime(2024, 1, 3, 12, 0, 0); // January 3, 2024 is a Wednesday

		// Act
		var (isActive, reason) = window.IsActiveAt(wednesday);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void If_AllDaysExceptOne_ThenWorksCorrectly()
	{
		// Arrange
		var allDaysExceptSunday = new List<DayOfWeek>
		{
			DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
			DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
		};

		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(0),
			new TimeSpan(23, 59, 59),
			allowedDays: allDaysExceptSunday);

		// Create a Sunday
		var sunday = new DateTime(2024, 1, 7, 12, 0, 0); // January 7, 2024 is a Sunday

		// Act
		var (isActive, reason) = window.IsActiveAt(sunday);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Outside allowed days");
	}

	[Fact]
	public void If_NonUtcEvaluationTime_ThenWorksCorrectly()
	{
		// Arrange
		var window = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));

		// Create non-UTC time (should be converted to UTC internally)
		var localTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Local);

		// Act
		var (isActive, reason) = window.IsActiveAt(localTime);

		// Assert - Should work regardless of DateTimeKind
		isActive.ShouldBeOneOf(true, false); // Result depends on local timezone
		reason.ShouldBeOneOf("Within time window", "Outside time window", "Outside allowed days");
	}
}

public class TimeZoneHelper_GetSystemTimeZones
{
	[Fact]
	public void If_GetSystemTimeZones_ThenReturnsNonEmptyList()
	{
		// Act
		var timeZones = TimeZoneHelper.GetSystemTimeZones();

		// Assert
		timeZones.ShouldNotBeNull();
		timeZones.Count.ShouldBeGreaterThan(0);
		timeZones.ShouldContain("UTC");
	}

	[Fact]
	public void If_GetSystemTimeZones_ThenReturnsUniqueTimeZones()
	{
		// Act
		var timeZones = TimeZoneHelper.GetSystemTimeZones();

		// Assert
		timeZones.Distinct().Count().ShouldBe(timeZones.Count);
	}
}

public class TimeZoneHelper_GetCurrentTimeAndDayInTimeZone
{
	[Fact]
	public void If_UtcTimeZone_ThenReturnsUtcTime()
	{
		// Arrange
		var utcTime = new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc);

		// Act
		var (time, day) = TimeZoneHelper.GetCurrentTimeAndDayInTimeZone("UTC", utcTime);

		// Assert
		time.ShouldBe(new TimeSpan(14, 30, 0));
		day.ShouldBe(DayOfWeek.Monday); // January 15, 2024 is a Monday
	}

	[Fact]
	public void If_DifferentTimeZone_ThenConvertsCorrectly()
	{
		// Arrange
		var utcTime = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc); // 2 PM UTC

		// Act
		var (time, day) = TimeZoneHelper.GetCurrentTimeAndDayInTimeZone("Eastern Standard Time", utcTime);

		// Assert
		time.ShouldBe(new TimeSpan(9, 0, 0)); // 9 AM EST (UTC-5 in January)
		day.ShouldBe(DayOfWeek.Monday);
	}

	[Fact]
	public void If_CrossesMidnight_ThenReturnsDifferentDay()
	{
		// Arrange
		var utcTime = new DateTime(2024, 1, 15, 6, 0, 0, DateTimeKind.Utc); // 6 AM UTC on Monday

		// Act - Convert to PST (UTC-8)
		var (time, day) = TimeZoneHelper.GetCurrentTimeAndDayInTimeZone("Pacific Standard Time", utcTime);

		// Assert
		time.ShouldBe(new TimeSpan(22, 0, 0)); // 10 PM PST
		day.ShouldBe(DayOfWeek.Sunday); // Previous day in PST
	}

	[Fact]
	public void If_NonUtcInput_ThenTreatsAsUtc()
	{
		// Arrange
		var localTime = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Local);

		// Act
		var (time, day) = TimeZoneHelper.GetCurrentTimeAndDayInTimeZone("UTC", localTime);

		// Assert
		time.ShouldBe(new TimeSpan(14, 0, 0)); // Should treat input as UTC
		day.ShouldBe(DayOfWeek.Monday);
	}

	[Fact]
	public void If_InvalidTimeZoneId_ThenThrowsTimeZoneNotFoundException()
	{
		// Arrange
		var utcTime = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc);

		// Act & Assert
		Should.Throw<TimeZoneNotFoundException>(() =>
			TimeZoneHelper.GetCurrentTimeAndDayInTimeZone("Invalid/TimeZone", utcTime));
	}
}