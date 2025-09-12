using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class ActivationSchedule_CreateSchedule
{
	[Fact]
	public void CreateSchedule_ValidFutureDate_CreatesSchedule()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);

		// Act
		var schedule = ActivationSchedule.CreateSchedule(enableDate);

		// Assert
		schedule.EnableOn.ShouldBe(enableDate);
		schedule.DisableOn.ShouldBe(DateTime.MaxValue);
		schedule.HasSchedule().ShouldBeTrue();
	}

	[Fact]
	public void CreateSchedule_WithDisableDate_CreatesScheduleWithBothDates()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);
		var disableDate = DateTime.UtcNow.AddDays(7);

		// Act
		var schedule = ActivationSchedule.CreateSchedule(enableDate, disableDate);

		// Assert
		schedule.EnableOn.ShouldBe(enableDate);
		schedule.DisableOn.ShouldBe(disableDate);
		schedule.HasSchedule().ShouldBeTrue();
	}

	[Fact]
	public void CreateSchedule_EnableDateInPast_ThrowsArgumentException()
	{
		// Arrange
		var pastDate = DateTime.UtcNow.AddDays(-1);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			ActivationSchedule.CreateSchedule(pastDate));
		exception.Message.ShouldBe("Scheduled enable date must be in the future.");
	}

	[Fact]
	public void CreateSchedule_InvalidEnableDate_ThrowsArgumentException()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			ActivationSchedule.CreateSchedule(DateTime.MinValue));
	}

	[Fact]
	public void CreateSchedule_DisableDateBeforeEnableDate_ThrowsArgumentException()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(7);
		var disableDate = DateTime.UtcNow.AddDays(1); // Before enable date

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			ActivationSchedule.CreateSchedule(enableDate, disableDate));
		exception.Message.ShouldBe("Scheduled disable date must be after the scheduled enable date.");
	}

	[Fact]
	public void CreateSchedule_DisableDateEqualsEnableDate_ThrowsArgumentException()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(1);
		var disableDate = enableDate;

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			ActivationSchedule.CreateSchedule(enableDate, disableDate));
	}
}

public class ActivationSchedule_Unscheduled
{
	[Fact]
	public void Unscheduled_HasBoundaryDateValues()
	{
		// Act
		var schedule = ActivationSchedule.Unscheduled;

		// Assert
		schedule.EnableOn.ShouldBe(DateTime.MinValue.ToUniversalTime());
		schedule.DisableOn.ShouldBe(DateTime.MaxValue.ToUniversalTime());
		schedule.HasSchedule().ShouldBeFalse();
	}

	[Fact]
	public void Unscheduled_IsNeverActive()
	{
		// Arrange
		var schedule = ActivationSchedule.Unscheduled;

		// Act
		var (isActive, reason) = schedule.IsActiveAt(DateTime.UtcNow);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("No active schedule set");
	}
}

public class ActivationSchedule_IsActiveAt
{
	[Fact]
	public void IsActiveAt_BeforeEnableDate_ReturnsFalse()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-1);
		var schedule = new ActivationSchedule(enableDate);
		var evaluationTime = DateTime.UtcNow.AddHours(-2); // Before enable

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public void IsActiveAt_AtEnableDate_ReturnsTrue()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-1);
		var schedule = new ActivationSchedule(enableDate);

		// Act
		var (isActive, reason) = schedule.IsActiveAt(enableDate);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public void IsActiveAt_AfterEnableNoDisable_ReturnsTrue()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-2);
		var schedule = new ActivationSchedule(enableDate);
		var evaluationTime = DateTime.UtcNow;

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public void IsActiveAt_BetweenEnableAndDisable_ReturnsTrue()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-2);
		var disableDate = DateTime.UtcNow.AddHours(2);
		var schedule = new ActivationSchedule(enableDate, disableDate);
		var evaluationTime = DateTime.UtcNow;

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeTrue();
		reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public void IsActiveAt_AtDisableDate_ReturnsFalse()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-2);
		var disableDate = DateTime.UtcNow;
		var schedule = new ActivationSchedule(enableDate, disableDate);

		// Act
		var (isActive, reason) = schedule.IsActiveAt(disableDate);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public void IsActiveAt_AfterDisableDate_ReturnsFalse()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-3);
		var disableDate = DateTime.UtcNow.AddHours(-1);
		var schedule = new ActivationSchedule(enableDate, disableDate);
		var evaluationTime = DateTime.UtcNow;

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBeFalse();
		reason.ShouldBe("Scheduled disable date passed");
	}

	[Theory]
	[InlineData(-24, -12, -6, false, "Scheduled disable date passed")] // All past, after disable
	[InlineData(-6, 6, 0, true, "Scheduled enable date reached")]      // Enable past, disable future
	[InlineData(6, 12, 0, false, "Scheduled enable date not reached")] // All future
	[InlineData(6, 12, 18, false, "Scheduled disable date passed")]    // Future enable/disable, eval after
	public void IsActiveAt_VariousTimeScenarios_ReturnsExpectedResults(
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
		var schedule = new ActivationSchedule(enableDate, disableDate);

		// Act
		var (isActive, reason) = schedule.IsActiveAt(evaluationTime);

		// Assert
		isActive.ShouldBe(expectedIsActive);
		reason.ShouldBe(expectedReason);
	}
}

public class ActivationSchedule_HasSchedule
{
	[Fact]
	public void HasSchedule_WithEnableDate_ReturnsTrue()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(-1);
		var schedule = new ActivationSchedule(enableDate);

		// Act & Assert
		schedule.HasSchedule().ShouldBeTrue();
	}

	[Fact]
	public void HasSchedule_WithBothDates_ReturnsTrue()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddDays(-1);
		var disableDate = DateTime.UtcNow.AddDays(1);
		var schedule = new ActivationSchedule(enableDate, disableDate);

		// Act & Assert
		schedule.HasSchedule().ShouldBeTrue();
	}

	[Fact]
	public void HasSchedule_Unscheduled_ReturnsFalse()
	{
		// Act & Assert
		ActivationSchedule.Unscheduled.HasSchedule().ShouldBeFalse();
	}
}