using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagActivationSchedule_EdgeCases
{
	[Fact]
	public void If_MultipleCalls_ThenConsistentResults()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-1);
		var disableDate = DateTime.UtcNow.AddHours(1);
		var schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate);
		var evaluationTime = DateTime.UtcNow;

		// Act - Multiple calls
		var result1 = schedule.IsActiveAt(evaluationTime);
		var result2 = schedule.IsActiveAt(evaluationTime);
		var result3 = schedule.IsActiveAt(evaluationTime);

		// Assert - All results should be identical
		result1.ShouldBe(result2);
		result2.ShouldBe(result3);
		result1.Item1.ShouldBeTrue();
		result1.Item2.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public void If_VerySmallTimeWindow_ThenWorksCorrectly()
	{
		// Arrange - 1 millisecond window
		var enableDate = DateTime.UtcNow.AddMilliseconds(100);
		var disableDate = enableDate.AddMilliseconds(1);
		var schedule = FlagActivationSchedule.CreateSchedule(enableDate, disableDate);

		// Act & Assert - Before window
		var beforeResult = schedule.IsActiveAt(enableDate.AddMilliseconds(-1));
		beforeResult.Item1.ShouldBeFalse();
		beforeResult.Item2.ShouldBe("Scheduled enable date not reached");

		// Act & Assert - At enable date
		var atEnableResult = schedule.IsActiveAt(enableDate);
		atEnableResult.Item1.ShouldBeTrue();
		atEnableResult.Item2.ShouldBe("Scheduled enable date reached");

		// Act & Assert - At disable date (should be disabled)
		var atDisableResult = schedule.IsActiveAt(disableDate);
		atDisableResult.Item1.ShouldBeFalse();
		atDisableResult.Item2.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public void If_LongTimeWindow_ThenWorksCorrectly()
	{
		// Arrange - 365 day window
		var enableDate = DateTime.UtcNow.AddDays(-100);
		var disableDate = enableDate.AddDays(365);
		var schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate);
		var evaluationTime = DateTime.UtcNow;

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public void If_TimeZoneChanges_ThenUtcConsistency()
	{
		// Arrange - Using UTC times to ensure consistency
		var utcNow = DateTime.UtcNow;
		var enableDate = utcNow.AddHours(-1);
		var disableDate = utcNow.AddHours(1);
		var schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate);

		// Act - Test with various evaluation times around the same UTC moment
		var results = new List<(bool, string)>
		{
			schedule.IsActiveAt(utcNow.AddMinutes(-5)),
			schedule.IsActiveAt(utcNow),
			schedule.IsActiveAt(utcNow.AddMinutes(5))
		};

		// Assert - All should be active since we're within the window
		results.ShouldAllBe(result => result.Item1 == true);
		results.ShouldAllBe(result => result.Item2 == "Scheduled enable date reached");
	}

	[Theory]
	[InlineData(1)]    // 1 second in future
	[InlineData(60)]   // 1 minute in future  
	[InlineData(3600)] // 1 hour in future
	[InlineData(86400)] // 1 day in future
	public void If_MinimalFutureDates_ThenCreatesSuccessfully(int secondsInFuture)
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddSeconds(secondsInFuture);

		// Act & Assert - Should not throw
		var schedule = Should.NotThrow(() => FlagActivationSchedule.CreateSchedule(enableDate));
		schedule.ScheduledEnableDate.ShouldBe(enableDate);
	}

	[Fact]
	public void If_CreateScheduleCalledMultipleTimes_ThenEachInstanceIsIndependent()
	{
		// Arrange
		var enableDate1 = DateTime.UtcNow.AddHours(1);
		var enableDate2 = DateTime.UtcNow.AddHours(2);
		var disableDate1 = DateTime.UtcNow.AddHours(3);

		// Act
		var schedule1 = FlagActivationSchedule.CreateSchedule(enableDate1, disableDate1);
		var schedule2 = FlagActivationSchedule.CreateSchedule(enableDate2);

		// Assert - Each instance should be independent
		schedule1.ScheduledEnableDate.ShouldBe(enableDate1);
		schedule1.ScheduledDisableDate.ShouldBe(disableDate1);
		
		schedule2.ScheduledEnableDate.ShouldBe(enableDate2);
		schedule2.ScheduledDisableDate.ShouldBeNull();

		// Modifications to one shouldn't affect the other
		schedule1.ShouldNotBe(schedule2);
	}
}

public class FlagActivationSchedule_PropertyAccess
{
	[Fact]
	public void If_PropertiesAccessed_ThenReturnCorrectValues()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);
		var disableDate = DateTime.UtcNow.AddDays(7);
		var schedule = FlagActivationSchedule.CreateSchedule(enableDate, disableDate);

		// Act & Assert - Properties should be accessible and return correct values
		schedule.ScheduledEnableDate.ShouldNotBeNull();
		schedule.ScheduledEnableDate.Value.ShouldBe(enableDate);
		schedule.ScheduledDisableDate.ShouldNotBeNull();
		schedule.ScheduledDisableDate.Value.ShouldBe(disableDate);
	}

	[Fact]
	public void If_UnscheduledProperties_ThenBothAreNull()
	{
		// Arrange
		var schedule = FlagActivationSchedule.Unscheduled;

		// Act & Assert
		schedule.ScheduledEnableDate.ShouldBeNull();
		schedule.ScheduledDisableDate.ShouldBeNull();
		schedule.ScheduledEnableDate.HasValue.ShouldBeFalse();
		schedule.ScheduledDisableDate.HasValue.ShouldBeFalse();
	}

	[Fact]
	public void If_OnlyEnableDateSet_ThenDisableDateIsNull()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);
		var schedule = FlagActivationSchedule.CreateSchedule(enableDate);

		// Act & Assert
		schedule.ScheduledEnableDate.ShouldNotBeNull();
		schedule.ScheduledEnableDate.Value.ShouldBe(enableDate);
		schedule.ScheduledDisableDate.ShouldBeNull();
		schedule.ScheduledDisableDate.HasValue.ShouldBeFalse();
	}
}

public class FlagActivationSchedule_StaticFactoryMethods
{
	[Fact]
	public void If_UnscheduledFactoryMethod_ThenReturnsConsistentInstance()
	{
		// Act
		var schedule1 = FlagActivationSchedule.Unscheduled;
		var schedule2 = FlagActivationSchedule.Unscheduled;

		// Assert - Both calls should return equivalent instances
		schedule1.ScheduledEnableDate.ShouldBe(schedule2.ScheduledEnableDate);
		schedule1.ScheduledDisableDate.ShouldBe(schedule2.ScheduledDisableDate);
		schedule1.HasSchedule().ShouldBe(schedule2.HasSchedule());
	}

	[Fact]
	public void If_CreateScheduleWithNullDisableDate_ThenDisableDateIsNull()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);

		// Act
		var schedule = FlagActivationSchedule.CreateSchedule(enableDate, null);

		// Assert
		schedule.ScheduledEnableDate.ShouldBe(enableDate);
		schedule.ScheduledDisableDate.ShouldBeNull();
	}
}