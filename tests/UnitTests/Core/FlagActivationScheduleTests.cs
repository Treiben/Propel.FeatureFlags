using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagActivationSchedule_Unscheduled
{
	[Fact]
	public void If_Unscheduled_ThenPropertiesAreNull()
	{
		// Arrange & Act
		var schedule = FlagActivationSchedule.Unscheduled;

		// Assert
		schedule.ScheduledEnableDate.ShouldBeNull();
		schedule.ScheduledDisableDate.ShouldBeNull();
	}

	[Fact]
	public void If_Unscheduled_ThenHasScheduleReturnsFalse()
	{
		// Arrange & Act
		var schedule = FlagActivationSchedule.Unscheduled;

		// Assert
		schedule.HasSchedule().ShouldBeFalse();
	}

	[Fact]
	public void If_Unscheduled_ThenIsActiveAtReturnsFalse()
	{
		// Arrange
		var schedule = FlagActivationSchedule.Unscheduled;
		var evaluationTime = DateTime.UtcNow;

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("No enable date set");
	}
}

public class FlagActivationSchedule_CreateSchedule
{
	[Fact]
	public void If_ValidFutureEnableDate_ThenCreatesSchedule()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);

		// Act
		var schedule = FlagActivationSchedule.CreateSchedule(enableDate);

		// Assert
		schedule.ScheduledEnableDate.ShouldBe(enableDate);
		schedule.ScheduledDisableDate.ShouldBeNull();
		schedule.HasSchedule().ShouldBeTrue();
	}

	[Fact]
	public void If_ValidEnableAndDisableDates_ThenCreatesScheduleWithBothDates()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);
		var disableDate = DateTime.UtcNow.AddDays(7);

		// Act
		var schedule = FlagActivationSchedule.CreateSchedule(enableDate, disableDate);

		// Assert
		schedule.ScheduledEnableDate.ShouldBe(enableDate);
		schedule.ScheduledDisableDate.ShouldBe(disableDate);
		schedule.HasSchedule().ShouldBeTrue();
	}

	[Theory]
	[InlineData("2000-01-01T00:00:00Z")] // Past date
	[InlineData("1900-01-01T00:00:00Z")] // Far past date
	public void If_EnableDateInPast_ThenThrowsArgumentException(string pastDateString)
	{
		// Arrange
		var pastDate = DateTime.Parse(pastDateString, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() => 
			FlagActivationSchedule.CreateSchedule(pastDate));
		exception.Message.ShouldBe("Scheduled enable date must be in the future.");
	}

	[Theory]
	[MemberData(nameof(GetInvalidDateValues))]
	public void If_InvalidEnableDate_ThenThrowsArgumentException(DateTime invalidDate, string expectedMessage)
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() => 
			FlagActivationSchedule.CreateSchedule(invalidDate));
		exception.Message.ShouldBe(expectedMessage);
	}

	[Fact]
	public void If_DisableDateBeforeEnableDate_ThenThrowsArgumentException()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(7);
		var disableDate = DateTime.UtcNow.AddDays(1); // Before enable date

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() => 
			FlagActivationSchedule.CreateSchedule(enableDate, disableDate));
		exception.Message.ShouldBe("Scheduled disable date must be after the scheduled enable date.");
	}

	[Fact]
	public void If_DisableDateEqualsEnableDate_ThenThrowsArgumentException()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);
		var disableDate = enableDate; // Same as enable date

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() => 
			FlagActivationSchedule.CreateSchedule(enableDate, disableDate));
		exception.Message.ShouldBe("Scheduled disable date must be after the scheduled enable date.");
	}

	public static IEnumerable<object[]> GetInvalidDateValues()
	{
		yield return new object[] { DateTime.MinValue, "Scheduled enable date must be a valid date." };
		yield return new object[] { DateTime.MaxValue, "Scheduled enable date must be a valid date." };
		yield return new object[] { DateTime.MinValue.AddTicks(1), "Scheduled enable date must be in the future." };
	}
}

public class FlagActivationSchedule_HasSchedule
{
	[Fact]
	public void If_OnlyEnableDateSet_ThenHasScheduleReturnsTrue()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);
		var schedule = FlagActivationSchedule.CreateSchedule(enableDate);

		// Act & Assert
		schedule.HasSchedule().ShouldBeTrue();
	}

	[Fact]
	public void If_BothDatesSet_ThenHasScheduleReturnsTrue()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);
		var disableDate = DateTime.UtcNow.AddDays(7);
		var schedule = FlagActivationSchedule.CreateSchedule(enableDate, disableDate);

		// Act & Assert
		schedule.HasSchedule().ShouldBeTrue();
	}

	[Fact]
	public void If_UnscheduledInstance_ThenHasScheduleReturnsFalse()
	{
		// Arrange
		var schedule = FlagActivationSchedule.Unscheduled;

		// Act & Assert
		schedule.HasSchedule().ShouldBeFalse();
	}

	[Fact]
	public void If_EnableDateIsMinValue_ThenHasScheduleReturnsFalse()
	{
		// This tests the internal constructor behavior indirectly
		// since we can't directly create instances with DateTime.MinValue through CreateSchedule
		var schedule = FlagActivationSchedule.Unscheduled;
		schedule.HasSchedule().ShouldBeFalse();
	}
}

public class FlagActivationSchedule_IsActiveAt
{
	[Fact]
	public void If_NoEnableDate_ThenReturnsFalseWithReason()
	{
		// Arrange
		var schedule = FlagActivationSchedule.Unscheduled;
		var evaluationTime = DateTime.UtcNow;

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("No enable date set");
	}

	[Fact]
	public void If_EvaluationTimeBeforeEnableDate_ThenReturnsFalseWithReason()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(2);
		var schedule = FlagActivationSchedule.CreateSchedule(enableDate);
		var evaluationTime = DateTime.UtcNow.AddHours(1); // Before enable date

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public void If_EvaluationTimeAfterEnableDateAndNoDisableDate_ThenReturnsTrueWithReason()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-1); // Past enable date
		var disableDate = DateTime.MaxValue; 
		var schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate);
		var evaluationTime = DateTime.UtcNow;

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public void If_EvaluationTimeAfterDisableDate_ThenReturnsFalseWithReason()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-2);
		var disableDate = DateTime.UtcNow.AddHours(-1); // Past disable date
		var schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate);
		var evaluationTime = DateTime.UtcNow;

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public void If_EvaluationTimeEqualsDisableDate_ThenReturnsFalseWithReason()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-1);
		var disableDate = DateTime.UtcNow;
		var schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate);
		var evaluationTime = disableDate; // Exactly at disable date

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public void If_EvaluationTimeBetweenEnableAndDisableDates_ThenReturnsTrueWithReason()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-2);
		var disableDate = DateTime.UtcNow.AddHours(2);
		var schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate);
		var evaluationTime = DateTime.UtcNow; // Between enable and disable dates

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public void If_EvaluationTimeExactlyAtEnableDate_ThenReturnsTrueWithReason()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(1);
		var schedule = FlagActivationSchedule.CreateSchedule(enableDate);
		var evaluationTime = enableDate; // Exactly at enable date

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Scheduled enable date reached");
	}

	[Theory]
	[InlineData(-24, -12, -6, false, "Scheduled disable date passed")] // All in past, after disable
	[InlineData(-12, -6, 0, false, "Scheduled disable date passed")]   // Enable/disable in past, eval at now
	[InlineData(-6, 6, 0, true, "Scheduled enable date reached")]      // Enable in past, disable in future, eval at now
	[InlineData(6, 12, 0, false, "Scheduled enable date not reached")] // All in future, eval at now
	[InlineData(6, 12, 18, false, "Scheduled disable date passed")]    // Enable/disable in future, eval after disable
	public void If_VariousTimeScenarios_ThenReturnsExpectedResults(
		double enableHoursOffset, 
		double disableHoursOffset, 
		double evaluationHoursOffset,
		bool expectedIsActive,
		string expectedReason)
	{
		// Arrange
		var baseTime = DateTime.UtcNow;
		var enableDate = baseTime.AddHours(enableHoursOffset);
		var disableDate = baseTime.AddHours(disableHoursOffset);
		var evaluationTime = baseTime.AddHours(evaluationHoursOffset);
		var schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate);

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBe(expectedIsActive);
		reason.ShouldBe(expectedReason);
	}
}