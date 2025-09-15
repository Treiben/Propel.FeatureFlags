using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Services.Evaluation;

namespace FeatureFlags.UnitTests.Evaluation;

public class OperationalWindowEvaluator_CanProcess
{
	private readonly OperationalWindowEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_FlagHasTimeWindowMode_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag { ActiveEvaluationModes = new EvaluationModes([EvaluationMode.TimeWindow]) };

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasMultipleModesIncludingTimeWindow_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag { ActiveEvaluationModes = new EvaluationModes([EvaluationMode.TimeWindow, EvaluationMode.Scheduled]) };

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Theory]
	[InlineData(EvaluationMode.Disabled)]
	[InlineData(EvaluationMode.Enabled)]
	[InlineData(EvaluationMode.Scheduled)]
	[InlineData(EvaluationMode.UserTargeted)]
	public void CanProcess_FlagDoesNotHaveTimeWindowMode_ReturnsFalse(EvaluationMode mode)
	{
		// Arrange
		var flag = new FeatureFlag { ActiveEvaluationModes = new EvaluationModes([mode]) };

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeFalse();
	}
}

public class OperationalWindowEvaluator_ProcessEvaluation
{
	private readonly OperationalWindowEvaluator _evaluator = new();

	[Fact]
	public async Task ProcessEvaluation_AlwaysOpen_ReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			OperationalWindow = OperationalWindow.AlwaysOpen,
			Variations = new Variations { DefaultVariation = "off" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(flag.Variations.DefaultVariation);
		result.Reason.ShouldBe("Flag operational window is always open.");
	}

	[Fact]
	public async Task ProcessEvaluation_WithinTimeWindow_ReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // Monday 12 PM
		var flag = new FeatureFlag
		{
			OperationalWindow = new OperationalWindow(
				TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
			Variations = new Variations { DefaultVariation = "window-closed" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: evaluationTime));

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(flag.Variations.DefaultVariation);
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task ProcessEvaluation_OutsideTimeWindow_ReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc); // Monday 8 PM
		var flag = new FeatureFlag
		{
			OperationalWindow = new OperationalWindow(
				TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
			Variations = new Variations { DefaultVariation = "window-closed" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: evaluationTime));

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("window-closed");
		result.Reason.ShouldBe("Outside time window");
	}

	[Fact]
	public async Task ProcessEvaluation_OvernightWindow_WorksCorrectly()
	{
		// Arrange
		var nightTime = new DateTime(2024, 1, 15, 2, 0, 0, DateTimeKind.Utc); // Monday 2 AM
		var dayTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // Monday 12 PM

		var flag = new FeatureFlag
		{
			OperationalWindow = new OperationalWindow(
				TimeSpan.FromHours(22), TimeSpan.FromHours(6)), // 10 PM to 6 AM
			Variations = new Variations { DefaultVariation = "maintenance-off" }
		};

		// Act
		var resultNight = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: nightTime));
		var resultDay = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: dayTime));

		// Assert
		resultNight.IsEnabled.ShouldBeTrue();
		resultNight.Reason.ShouldBe("Within time window");

		resultDay.IsEnabled.ShouldBeFalse();
		resultDay.Reason.ShouldBe("Outside time window");
	}

	[Fact]
	public async Task ProcessEvaluation_DayOfWeekFiltering_WorksCorrectly()
	{
		// Arrange
		var monday = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // Monday 12 PM
		var saturday = new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Utc); // Saturday 12 PM
		DayOfWeek[] weekdaysOnly = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
			DayOfWeek.Thursday, DayOfWeek.Friday];

		var flag = new FeatureFlag
		{
			OperationalWindow = new OperationalWindow(
				TimeSpan.FromHours(9), TimeSpan.FromHours(17),
				daysActive: weekdaysOnly),
			Variations = new Variations { DefaultVariation = "weekend-off" }
		};

		// Act
		var resultMonday = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: monday));
		var resultSaturday = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: saturday));

		// Assert
		resultMonday.IsEnabled.ShouldBeTrue();
		resultMonday.Reason.ShouldBe("Within time window");

		resultSaturday.IsEnabled.ShouldBeFalse();
		resultSaturday.Variation.ShouldBe("weekend-off");
		resultSaturday.Reason.ShouldBe("Outside allowed days");
	}

	[Fact]
	public async Task ProcessEvaluation_TimeZoneHandling_WorksCorrectly()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc); // 5 PM UTC
		var flag = new FeatureFlag
		{
			OperationalWindow = new OperationalWindow(
				TimeSpan.FromHours(9), TimeSpan.FromHours(17),
				"Pacific Standard Time"), // Window is in PST
			Variations = new Variations { DefaultVariation = "off" }
		};

		// Act - Use Eastern time context (5 PM UTC = 12 PM EST, within window)
		var result = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: evaluationTime, timeZone: "Eastern Standard Time"));

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task ProcessEvaluation_NoEvaluationTimeProvided_UsesCurrentTime()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			OperationalWindow = OperationalWindow.AlwaysOpen,
			Variations = new Variations { DefaultVariation = "off" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: null));

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Flag operational window is always open.");
	}

	[Fact]
	public async Task ProcessEvaluation_BusinessHoursScenario_WorksCorrectly()
	{
		// Arrange - Business hours: 9 AM - 5 PM, weekdays only
		DayOfWeek[] businessDays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
			DayOfWeek.Thursday, DayOfWeek.Friday];
		var flag = new FeatureFlag
		{
			OperationalWindow = new OperationalWindow(
				TimeSpan.FromHours(9), TimeSpan.FromHours(17),
				daysActive: businessDays),
			Variations = new Variations { DefaultVariation = "after-hours" }
		};

		// Act & Assert - During business hours (Wednesday 2 PM)
		var businessResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: new DateTime(2024, 1, 17, 14, 0, 0, DateTimeKind.Utc)));
		businessResult.IsEnabled.ShouldBeTrue();
		businessResult.Reason.ShouldBe("Within time window");

		// Act & Assert - After hours (Wednesday 8 PM)
		var afterResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: new DateTime(2024, 1, 17, 20, 0, 0, DateTimeKind.Utc)));
		afterResult.IsEnabled.ShouldBeFalse();
		afterResult.Reason.ShouldBe("Outside time window");

		// Act & Assert - Weekend (Saturday 2 PM)
		var weekendResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: new DateTime(2024, 1, 20, 14, 0, 0, DateTimeKind.Utc)));
		weekendResult.IsEnabled.ShouldBeFalse();
		weekendResult.Reason.ShouldBe("Outside allowed days");
	}
}