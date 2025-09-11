using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class OperationalWindow_Validation
{
	[Theory]
	[InlineData(-1, 0, 0)]
	[InlineData(24, 0, 0)]
	public void CreateWindow_InvalidStartTime_ThrowsArgumentException(int hours, int minutes, int seconds)
	{
		// Arrange
		var invalidStartTime = new TimeSpan(hours, minutes, seconds);
		var endTime = TimeSpan.FromHours(17);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			OperationalWindow.CreateWindow(invalidStartTime, endTime));
		exception.ParamName.ShouldBe("startTime");
	}

	[Theory]
	[InlineData(-1, 0, 0)]
	[InlineData(24, 0, 0)]
	public void CreateWindow_InvalidEndTime_ThrowsArgumentException(int hours, int minutes, int seconds)
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var invalidEndTime = new TimeSpan(hours, minutes, seconds);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			OperationalWindow.CreateWindow(startTime, invalidEndTime));
		exception.ParamName.ShouldBe("endTime");
	}

	[Fact]
	public void CreateWindow_InvalidTimeZone_ThrowsArgumentException()
	{
		// Arrange
		var startTime = TimeSpan.FromHours(9);
		var endTime = TimeSpan.FromHours(17);

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			OperationalWindow.CreateWindow(startTime, endTime, "Invalid/TimeZone"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void CreateWindow_NullOrEmptyTimeZone_DefaultsToUtc(string? timeZone)
	{
		// Act
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17), timeZone);

		// Assert
		window.TimeZone.ShouldBe("UTC");
	}

	[Fact]
	public void CreateWindow_DuplicateAllowedDays_RemovesDuplicates()
	{
		// Arrange
		var duplicateDays = new List<DayOfWeek>
		{
			DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Monday
		};

		// Act
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17), allowedDays: duplicateDays);

		// Assert
		window.WindowDays.Length.ShouldBe(2);
		window.WindowDays.ShouldContain(DayOfWeek.Monday);
		window.WindowDays.ShouldContain(DayOfWeek.Tuesday);
	}
}

public class OperationalWindow_AlwaysOpen
{
	[Fact]
	public void AlwaysOpen_ReturnsFullDayWindow()
	{
		// Act
		var window = OperationalWindow.AlwaysOpen;

		// Assert
		window.WindowStartTime.ShouldBe(TimeSpan.Zero);
		window.WindowEndTime.ShouldBe(new TimeSpan(23, 59, 59));
		window.TimeZone.ShouldBe("UTC");
		window.WindowDays.Length.ShouldBe(7);
		window.HasWindow().ShouldBeFalse(); // Zero start time = no window
	}
}

public class OperationalWindow_IsActiveAt
{
	[Fact]
	public void IsActiveAt_WithinSameDayWindow_ReturnsTrue()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17));
		var evaluationTime = DateTime.UtcNow.Date.AddHours(12); // 12 PM

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void IsActiveAt_OutsideSameDayWindow_ReturnsFalse()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17));
		var evaluationTime = DateTime.UtcNow.Date.AddHours(20); // 8 PM

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Outside time window");
	}

	[Fact]
	public void IsActiveAt_WithinOvernightWindow_ReturnsTrue()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(22), TimeSpan.FromHours(6)); // 10 PM to 6 AM
		var evaluationTime = DateTime.UtcNow.Date.AddHours(2); // 2 AM

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void IsActiveAt_OutsideOvernightWindow_ReturnsFalse()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(22), TimeSpan.FromHours(6)); // 10 PM to 6 AM
		var evaluationTime = DateTime.UtcNow.Date.AddHours(12); // 12 PM

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Outside time window");
	}

	[Fact]
	public void IsActiveAt_AllowedDay_ReturnsTrue()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17),
			allowedDays: [DayOfWeek.Monday]);

		// Create a Monday at 12 PM
		var monday = new DateTime(2024, 1, 1, 12, 0, 0); // Jan 1, 2024 is Monday

		// Act
		var (isActive, reason) = window.IsActiveAt(monday);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void IsActiveAt_DisallowedDay_ReturnsFalse()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17),
			allowedDays: [DayOfWeek.Monday]);

		// Create a Tuesday at 12 PM
		var tuesday = new DateTime(2024, 1, 2, 12, 0, 0); // Jan 2, 2024 is Tuesday

		// Act
		var (isActive, reason) = window.IsActiveAt(tuesday);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Outside allowed days");
	}

	[Fact]
	public void IsActiveAt_InvalidTimeZone_ReturnsFalse()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17));

		// Act
		var (isActive, reason) = window.IsActiveAt(DateTime.UtcNow, "Invalid/TimeZone");

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Invalid timezone: Invalid/TimeZone");
	}

	[Fact]
	public void IsActiveAt_ContextTimeZoneOverridesWindowTimeZone()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17),
			"Pacific Standard Time");

		// 5 PM UTC = 12 PM EST (within window)
		var evaluationTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc);

		// Act - Override with Eastern time
		var (isActive, reason) = window.IsActiveAt(evaluationTime, "Eastern Standard Time");

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}
}

public class OperationalWindow_TimeZoneHandling
{
	[Fact]
	public void CreateWindow_ValidTimeZones_CreatesSuccessfully()
	{
		// Arrange
		string[] validTimeZones = ["UTC", "Eastern Standard Time", "Pacific Standard Time"];

		foreach (var timeZone in validTimeZones)
		{
			// Act & Assert
			var window = Should.NotThrow(() => OperationalWindow.CreateWindow(
				TimeSpan.FromHours(9), TimeSpan.FromHours(17), timeZone));
			window.TimeZone.ShouldBe(timeZone);
		}
	}

	[Fact]
	public void IsActiveAt_TimeZoneConversion_WorksCorrectly()
	{
		// Arrange - Window in Eastern Time
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17),
			"Eastern Standard Time");

		// 2 PM UTC in January = 9 AM EST (start of window)
		var evaluationTime = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc);

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}
}

public class OperationalWindow_EdgeCases
{
	[Fact]
	public void IsActiveAt_ExactStartTime_ReturnsTrue()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9).Add(TimeSpan.FromMinutes(30)),
			TimeSpan.FromHours(17));
		var evaluationTime = DateTime.UtcNow.Date.AddHours(9).AddMinutes(30);

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void IsActiveAt_ExactEndTime_ReturnsTrue()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17).Add(TimeSpan.FromMinutes(30)));
		var evaluationTime = DateTime.UtcNow.Date.AddHours(17).AddMinutes(30);

		// Act
		var (isActive, reason) = window.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Within time window");
	}

	[Fact]
	public void HasWindow_ZeroStartTime_ReturnsFalse()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.Zero, TimeSpan.FromHours(6));

		// Act & Assert
		window.HasWindow().ShouldBeFalse();
	}

	[Fact]
	public void HasWindow_NonZeroTimes_ReturnsTrue()
	{
		// Arrange
		var window = OperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17));

		// Act & Assert
		window.HasWindow().ShouldBeTrue();
	}
}

public class TimeZoneHelper_Tests
{
	[Fact]
	public void GetCurrentTimeAndDayInTimeZone_UtcTimeZone_ReturnsUtcTime()
	{
		// Arrange
		var utcTime = new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc);

		// Act
		var (time, day) = TimeZoneHelper.GetCurrentTimeAndDayInTimeZone("UTC", utcTime);

		// Assert
		time.ShouldBe(new TimeSpan(14, 30, 0));
		day.ShouldBe(DayOfWeek.Monday); // Jan 15, 2024 is Monday
	}

	[Fact]
	public void GetCurrentTimeAndDayInTimeZone_DifferentTimeZone_ConvertsCorrectly()
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
	public void GetCurrentTimeAndDayInTimeZone_InvalidTimeZone_ThrowsException()
	{
		// Arrange
		var utcTime = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc);

		// Act & Assert
		Should.Throw<TimeZoneNotFoundException>(() =>
			TimeZoneHelper.GetCurrentTimeAndDayInTimeZone("Invalid/TimeZone", utcTime));
	}
}