using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace FeatureFlags.UnitTests.Evaluation.Handlers;

public class ActivationScheduleEvaluator_CanProcess
{
	private readonly ActivationScheduleEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_FlagHasScheduledMode_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag { ActiveEvaluationModes = new EvaluationModes() };
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.Scheduled);

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasMultipleModesIncludingScheduled_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag { ActiveEvaluationModes = new EvaluationModes() };
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.Scheduled);
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.TimeWindow);

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Theory]
	[InlineData(EvaluationMode.Disabled)]
	[InlineData(EvaluationMode.Enabled)]
	[InlineData(EvaluationMode.TimeWindow)]
	[InlineData(EvaluationMode.UserTargeted)]
	public void CanProcess_FlagDoesNotHaveScheduledMode_ReturnsFalse(EvaluationMode mode)
	{
		// Arrange
		var flag = new FeatureFlag { ActiveEvaluationModes = new EvaluationModes() };
		flag.ActiveEvaluationModes.AddMode(mode);

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
		var flag = new FeatureFlag
		{
			Schedule = ActivationSchedule.Unscheduled,
			Variations = new Variations { DefaultVariation = "off" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag has no activation schedule and can be available immediately.");
	}

	[Fact]
	public async Task ProcessEvaluation_BeforeEnableDate_ReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

		var flag = new FeatureFlag
		{
			Schedule = new ActivationSchedule(enableDate),
			Variations = new Variations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("scheduled-off");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task ProcessEvaluation_AtEnableDate_ReturnsEnabled()
	{
		// Arrange
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

		var flag = new FeatureFlag
		{
			Schedule = new ActivationSchedule(enableDate),
			Variations = new Variations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: enableDate);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task ProcessEvaluation_AfterEnableDate_ReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

		var flag = new FeatureFlag
		{
			Schedule = new ActivationSchedule(enableDate),
			Variations = new Variations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task ProcessEvaluation_BetweenEnableAndDisable_ReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 17, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var disableDate = new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc);

		var flag = new FeatureFlag
		{
			Schedule = new ActivationSchedule(enableDate, disableDate),
			Variations = new Variations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task ProcessEvaluation_AtDisableDate_ReturnsDisabled()
	{
		// Arrange
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var disableDate = new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc);

		var flag = new FeatureFlag
		{
			Schedule = new ActivationSchedule(enableDate, disableDate),
			Variations = new Variations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: disableDate);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("scheduled-off");
		result.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task ProcessEvaluation_AfterDisableDate_ReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 25, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var disableDate = new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc);

		var flag = new FeatureFlag
		{
			Schedule = new ActivationSchedule(enableDate, disableDate),
			Variations = new Variations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("scheduled-off");
		result.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task ProcessEvaluation_NoEvaluationTimeProvided_UsesCurrentTime()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-1); // 1 hour ago

		var flag = new FeatureFlag
		{
			Schedule = new ActivationSchedule(enableDate),
			Variations = new Variations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: null);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task ProcessEvaluation_FeatureLaunchWindow_WorksCorrectly()
	{
		// Arrange - Feature launches and ends one week later
		var launchDate = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc);
		var endDate = new DateTime(2024, 1, 22, 9, 0, 0, DateTimeKind.Utc);

		var flag = new FeatureFlag
		{
			Schedule = new ActivationSchedule(launchDate, endDate),
			Variations = new Variations { DefaultVariation = "feature-disabled" }
		};

		// Act & Assert - Before launch
		var resultBefore = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: launchDate.AddMinutes(-1)));
		resultBefore.IsEnabled.ShouldBeFalse();
		resultBefore.Variation.ShouldBe("feature-disabled");

		// Act & Assert - During active period
		var resultDuring = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: launchDate.AddDays(3)));
		resultDuring.IsEnabled.ShouldBeTrue();
		resultDuring.Variation.ShouldBe("on");

		// Act & Assert - After end
		var resultAfter = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(evaluationTime: endDate.AddMinutes(1)));
		resultAfter.IsEnabled.ShouldBeFalse();
		resultAfter.Variation.ShouldBe("feature-disabled");
	}
}