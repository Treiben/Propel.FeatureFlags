using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace UnitTests.Evaluators;

public class OperationalWindowEvaluator_CanProcess
{
	private readonly OperationalWindowEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_FlagHasTimeWindowMode_ReturnsTrue()
	{
		// Arrange
		var identifier = new GlobalFlagIdentifier("test-flag");
		var modes = new ModeSet([EvaluationMode.TimeWindow]);
		var flagConfig = new EvaluationOptions(
			key: identifier.Key, modeSet: modes);

		// Act & Assert
		_evaluator.CanProcess(flagConfig, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasMultipleModesIncludingTimeWindow_ReturnsTrue()
	{
		// Arrange
		var identifier = new GlobalFlagIdentifier("test-flag");
		var modes = new ModeSet([EvaluationMode.TimeWindow, EvaluationMode.Scheduled]);
		var flagConfig = new EvaluationOptions(
			key: identifier.Key, modeSet: modes);

		// Act & Assert
		_evaluator.CanProcess(flagConfig, new EvaluationContext()).ShouldBeTrue();
	}

	[Theory]
	[InlineData(EvaluationMode.Off)]
	[InlineData(EvaluationMode.On)]
	[InlineData(EvaluationMode.Scheduled)]
	[InlineData(EvaluationMode.UserTargeted)]
	public void CanProcess_FlagDoesNotHaveTimeWindowMode_ReturnsFalse(EvaluationMode mode)
	{
		// Arrange
		var identifier = new GlobalFlagIdentifier("test-flag");
		var modes = new ModeSet([mode]);
		var flagConfig = new EvaluationOptions(
			key: identifier.Key, modeSet: modes);

		// Act & Assert
		_evaluator.CanProcess(flagConfig, new EvaluationContext()).ShouldBeFalse();
	}
}

public class OperationalWindowEvaluator_ProcessEvaluation
{
	private readonly OperationalWindowEvaluator _evaluator = new();

	[Fact]
	public async Task ProcessEvaluation_AlwaysOpen_ReturnsEnabled()
	{
		// Arrange
		var identifier = new GlobalFlagIdentifier("test-flag");
		var flagConfig = new EvaluationOptions(identifier.Key);

		// Act
		var result = await _evaluator.Evaluate(flagConfig, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Flag operational window is always open.");
	}

	[Fact]
	public async Task ProcessEvaluation_WithinTimeWindow_ReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new UtcDateTime(new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc)); // Monday 12 PM

		var identifier = new GlobalFlagIdentifier("test-flag");
		var window = new UtcTimeWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17)); // 9 AM to 5 PM
		var variations = new Variations { DefaultVariation = "window-open" };
		var flagConfig = new EvaluationOptions(key: identifier.Key, operationalWindow: window, variations: variations);

		// Act
		var result = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(evaluationTime: evaluationTime));

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(flagConfig.Variations.DefaultVariation);
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task ProcessEvaluation_OutsideTimeWindow_ReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new UtcDateTime(new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc)); // Monday 8 PM

		var identifier = new GlobalFlagIdentifier("test-flag");
		var window = new UtcTimeWindow(
			TimeSpan.FromHours(9), TimeSpan.FromHours(17)); // 9 AM to 5 PM
		var variations = new Variations { DefaultVariation = "window-closed" };
		var flagConfig = new EvaluationOptions(key: identifier.Key, operationalWindow: window, variations: variations);

		// Act
		var result = await _evaluator.Evaluate(flagConfig,
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
		var nightTime = new UtcDateTime(new DateTime(2024, 1, 15, 2, 0, 0, DateTimeKind.Local)); // Monday 8 AM UTC
		var dayTime = new UtcDateTime(new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc)); // Monday 12 PM UTC

		var identifier = new GlobalFlagIdentifier("test-flag");
		var window = new UtcTimeWindow(
				TimeSpan.FromHours(22), TimeSpan.FromHours(10)); // 10 PM to 10 AM
		var variations = new Variations { DefaultVariation = "maintenance-off" };
		var flagConfig = new EvaluationOptions(key: identifier.Key, operationalWindow: window, variations: variations);


		// Act
		var resultNight = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(evaluationTime: nightTime));
		var resultDay = await _evaluator.Evaluate(flagConfig,
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
		var dt = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc); // Monday 9 AM UTC
		var monday = new UtcDateTime(dt); // Monday 9 UTC
		var saturday = new UtcDateTime(new DateTime(2024, 1, 20, 9, 0, 0, DateTimeKind.Unspecified)); // Saturday 3 PM UTC
		DayOfWeek[] weekdaysOnly = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
			DayOfWeek.Thursday, DayOfWeek.Friday];

		var identifier = new GlobalFlagIdentifier("test-flag");
		var window = new UtcTimeWindow(
				TimeSpan.FromHours(9), TimeSpan.FromHours(17),
				daysActive: weekdaysOnly);
		var variations = new Variations { DefaultVariation = "weekend-off" };
		var flagConfig = new EvaluationOptions(key: identifier.Key, operationalWindow: window, variations: variations);

		// Act
		var resultMonday = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(evaluationTime: monday));
		var resultSaturday = await _evaluator.Evaluate(flagConfig,
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

		var identifier = new GlobalFlagIdentifier("test-flag");
		var window = new UtcTimeWindow(
				TimeSpan.FromHours(9), TimeSpan.FromHours(17),
				"Pacific Standard Time"); // Window is in PST
		var variations = new Variations { DefaultVariation = "off" };
		var flagConfig = new EvaluationOptions(key: identifier.Key, operationalWindow: window, variations: variations);

		// Act - Use Eastern time context (5 PM UTC = 12 PM EST, within window)
		var result = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(evaluationTime: new UtcDateTime(evaluationTime)));

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task ProcessEvaluation_NoEvaluationTimeProvided_UsesCurrentTime()
	{
		// Arrange
		var identifier = new GlobalFlagIdentifier("test-flag");
		var flagConfig = new EvaluationOptions(identifier.Key);

		// Act
		var result = await _evaluator.Evaluate(flagConfig,
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

		var identifier = new GlobalFlagIdentifier("test-flag");
		var window = new UtcTimeWindow(
				TimeSpan.FromHours(9), TimeSpan.FromHours(17),
				daysActive: businessDays); 
		var variations = new Variations { DefaultVariation = "after-hours" };
		var flagConfig = new EvaluationOptions(key: identifier.Key, operationalWindow: window, variations: variations);

		// Act & Assert - During business hours (Wednesday 2 PM)
		var businessResult = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(evaluationTime: new UtcDateTime(new DateTime(2024, 1, 17, 14, 0, 0, DateTimeKind.Utc))));
		businessResult.IsEnabled.ShouldBeTrue();
		businessResult.Reason.ShouldBe("Within time window");

		// Act & Assert - After hours (Wednesday 8 PM)
		var afterResult = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(evaluationTime: new UtcDateTime(new DateTime(2024, 1, 17, 20, 0, 0, DateTimeKind.Utc))));
		afterResult.IsEnabled.ShouldBeFalse();
		afterResult.Reason.ShouldBe("Outside time window");

		// Act & Assert - Weekend (Saturday 2 PM)
		var weekendResult = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(evaluationTime: new UtcDateTime(new DateTime(2024, 1, 20, 14, 0, 0, DateTimeKind.Utc))));
		weekendResult.IsEnabled.ShouldBeFalse();
		weekendResult.Reason.ShouldBe("Outside allowed days");
	}
}