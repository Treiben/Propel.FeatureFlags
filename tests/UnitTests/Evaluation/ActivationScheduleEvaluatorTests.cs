using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace FeatureFlags.UnitTests.Evaluation;

public class ActivationScheduleEvaluator_CanProcess
{
	private readonly ActivationScheduleEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_FlagHasScheduledMode_ReturnsTrue()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(key: identifier.Key, modeSet: new ModeSet([EvaluationMode.Scheduled]));

		// Act & Assert
		_evaluator.CanProcess(flagConfig, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasMultipleModesIncludingScheduled_ReturnsTrue()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var modes = new ModeSet([EvaluationMode.Scheduled, EvaluationMode.TimeWindow]);
		var flag = new EvaluationOptions(key: identifier.Key, modeSet: modes);
		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Theory]
	[InlineData(EvaluationMode.Off)]
	[InlineData(EvaluationMode.On)]
	[InlineData(EvaluationMode.TimeWindow)]
	[InlineData(EvaluationMode.UserTargeted)]
	public void CanProcess_FlagDoesNotHaveScheduledMode_ReturnsFalse(EvaluationMode mode)
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var modes = new ModeSet([mode]);
		var flag = new EvaluationOptions(key: identifier.Key, modeSet: modes);

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeFalse();
	}
}

public class ActivationScheduleEvaluator_ProcessEvaluation
{
	private readonly ActivationScheduleEvaluator _evaluator = new();

	[Fact]
	public async Task ProcessEvaluation_NoSchedule_EnablesImmediately()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(key: identifier.Key);

		// Act
		var result = await _evaluator.Evaluate(flagConfig, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Flag has no activation schedule and can be available immediately.");
	}

	[Fact]
	public async Task ProcessEvaluation_BeforeEnableDate_ReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				schedule: new UtcSchedule(new UtcDateTime(enableDate), UtcDateTime.MaxValue),
				variations: new Variations { DefaultVariation = "scheduled-off" }
			);

		var context = new EvaluationContext(evaluationTime: new UtcDateTime (evaluationTime));

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("scheduled-off");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task ProcessEvaluation_AtEnableDate_ReturnsEnabled()
	{
		// Arrange
		var enableDate = new UtcDateTime(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var schedule = new UtcSchedule(new UtcDateTime(enableDate), UtcDateTime.MaxValue);
		var variations = new Variations { DefaultVariation = "scheduled-off" };
		var flag = new EvaluationOptions(key: identifier.Key, schedule: schedule, variations: variations);

		var context = new EvaluationContext(evaluationTime: enableDate);

		// Act
		var result = await _evaluator.Evaluate(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(flag.Variations.DefaultVariation);
	}

	[Fact]
	public async Task ProcessEvaluation_AfterEnableDate_ReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new UtcDateTime(new DateTime(2024, 1, 20, 12, 0, 0));
		var enableDate = new UtcDateTime(new DateTime(2024, 1, 15, 10, 0, 0));

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				schedule: new UtcSchedule(enableDate, UtcDateTime.MaxValue),
				variations: new Variations { DefaultVariation = "scheduled-off" }
			);

		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(flagConfig.Variations.DefaultVariation);
	}

	[Fact]
	public async Task ProcessEvaluation_BetweenEnableAndDisable_ReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new UtcDateTime(new DateTime(2024, 1, 17, 12, 0, 0, DateTimeKind.Utc));
		var enableDate = new UtcDateTime(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));
		var disableDate = new UtcDateTime(new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc));

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				schedule: new UtcSchedule(enableDate, disableDate),
				variations: new Variations { DefaultVariation = "scheduled-off" }
			);

		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(flagConfig.Variations.DefaultVariation);
	}

	[Fact]
	public async Task ProcessEvaluation_AtDisableDate_ReturnsDisabled()
	{
		// Arrange
		var enableDate = new UtcDateTime(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));
		var disableDate = new UtcDateTime(new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc));

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				schedule: new UtcSchedule(enableDate, disableDate),
				variations: new Variations { DefaultVariation = "scheduled-off" }
			);

		var context = new EvaluationContext(evaluationTime: disableDate);

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(flagConfig.Variations.DefaultVariation);
		result.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task ProcessEvaluation_AfterDisableDate_ReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new UtcDateTime(new DateTime(2024, 1, 25, 12, 0, 0, DateTimeKind.Utc));
		var enableDate = new UtcDateTime(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));
		var disableDate = new UtcDateTime(new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc));

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				schedule: new UtcSchedule(enableDate, disableDate),
				variations: new Variations { DefaultVariation = "scheduled-off" }
			);

		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(flagConfig.Variations.DefaultVariation);
		result.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task ProcessEvaluation_NoEvaluationTimeProvided_UsesCurrentTime()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-1); // 1 hour ago

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				schedule: new UtcSchedule(new UtcDateTime(enableDate), UtcDateTime.MaxValue),
				variations: new Variations { DefaultVariation = "off" }
			);

		var context = new EvaluationContext(evaluationTime: null);

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task ProcessEvaluation_FeatureLaunchWindow_WorksCorrectly()
	{
		// Arrange - Feature launches and ends one week later
		var launchDate = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Unspecified);
		var endDate = new DateTime(2024, 1, 22, 9, 0, 0, DateTimeKind.Unspecified);

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				schedule: new UtcSchedule(new UtcDateTime(launchDate), new UtcDateTime(endDate)),
				variations: new Variations { DefaultVariation = "feature-disabled" }
			);

		// Act & Assert - Before launch
		var resultBefore = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(evaluationTime: new UtcDateTime(launchDate.AddMinutes(-1))));
		resultBefore.IsEnabled.ShouldBeFalse();
		resultBefore.Variation.ShouldBe(flagConfig.Variations.DefaultVariation);

		// Act & Assert - During active period
		var resultDuring = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(evaluationTime: new UtcDateTime(launchDate.AddDays(3))));
		resultDuring.IsEnabled.ShouldBeTrue();
		resultDuring.Variation.ShouldBe(flagConfig.Variations.DefaultVariation);

		// Act & Assert - After end
		var resultAfter = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(evaluationTime: new UtcDateTime(endDate.AddMinutes(1))));
		resultAfter.IsEnabled.ShouldBeFalse();
		resultAfter.Variation.ShouldBe(flagConfig.Variations.DefaultVariation);
	}
}