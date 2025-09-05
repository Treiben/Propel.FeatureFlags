using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace FeatureFlags.UnitTests.Evaluation.Handlers;

public class ActivationScheduleEvaluator_EvaluationOrder
{
	[Fact]
	public void EvaluationOrder_ShouldReturnActivationSchedule()
	{
		// Arrange
		var evaluator = new ActivationScheduleEvaluator();

		// Act
		var order = evaluator.EvaluationOrder;

		// Assert
		order.ShouldBe(EvaluationOrder.ActivationSchedule);
	}
}

public class ActivationScheduleEvaluator_CanProcess
{
	private readonly ActivationScheduleEvaluator _evaluator;

	public ActivationScheduleEvaluator_CanProcess()
	{
		_evaluator = new ActivationScheduleEvaluator();
	}

	[Fact]
	public void If_FlagContainsScheduledMode_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_FlagContainsMultipleModesIncludingScheduled_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData(FlagEvaluationMode.Disabled)]
	[InlineData(FlagEvaluationMode.Enabled)]
	[InlineData(FlagEvaluationMode.TimeWindow)]
	[InlineData(FlagEvaluationMode.UserTargeted)]
	[InlineData(FlagEvaluationMode.UserRolloutPercentage)]
	public void If_FlagDoesNotContainScheduledMode_ThenCannotProcess(FlagEvaluationMode mode)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(mode);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_FlagHasNoModes_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}
}

public class ActivationScheduleEvaluator_ProcessEvaluation_ScheduleValidation
{
	private readonly ActivationScheduleEvaluator _evaluator;

	public ActivationScheduleEvaluator_ProcessEvaluation_ScheduleValidation()
	{
		_evaluator = new ActivationScheduleEvaluator();
	}

	[Fact]
	public async Task If_FlagHasNoSchedule_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.Unscheduled,
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext();

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("The flag's activation schedule is not setup.");
	}

	[Fact]
	public async Task If_ScheduleHasNoEnableDate_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(null, null),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext();

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("The flag's activation schedule is not setup.");
	}
}

public class ActivationScheduleEvaluator_ProcessEvaluation_EvaluationTime
{
	private readonly ActivationScheduleEvaluator _evaluator;

	public ActivationScheduleEvaluator_ProcessEvaluation_EvaluationTime()
	{
		_evaluator = new ActivationScheduleEvaluator();
	}

	[Fact]
	public async Task If_EvaluationTimeProvided_ThenUsesProvidedTime()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 10, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, null),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_EvaluationTimeNotProvided_ThenUsesCurrentTime()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(-1); // 1 hour ago
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, null),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: null);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Scheduled enable date reached");
	}
}

public class ActivationScheduleEvaluator_ProcessEvaluation_EnableDateLogic
{
	private readonly ActivationScheduleEvaluator _evaluator;

	public ActivationScheduleEvaluator_ProcessEvaluation_EnableDateLogic()
	{
		_evaluator = new ActivationScheduleEvaluator();
	}

	[Fact]
	public async Task If_BeforeEnableDate_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, null),
			Variations = new FlagVariations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("scheduled-off");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_ExactlyAtEnableDate_ThenReturnsEnabled()
	{
		// Arrange
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, null),
			Variations = new FlagVariations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: enableDate);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_AfterEnableDate_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, null),
			Variations = new FlagVariations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}
}

public class ActivationScheduleEvaluator_ProcessEvaluation_DisableDateLogic
{
	private readonly ActivationScheduleEvaluator _evaluator;

	public ActivationScheduleEvaluator_ProcessEvaluation_DisableDateLogic()
	{
		_evaluator = new ActivationScheduleEvaluator();
	}

	[Fact]
	public async Task If_BetweenEnableAndDisableDates_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 17, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var disableDate = new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate),
			Variations = new FlagVariations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_ExactlyAtDisableDate_ThenReturnsDisabled()
	{
		// Arrange
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var disableDate = new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate),
			Variations = new FlagVariations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: disableDate);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("scheduled-off");
		result.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task If_AfterDisableDate_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 25, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var disableDate = new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate),
			Variations = new FlagVariations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("scheduled-off");
		result.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task If_BeforeEnableDateWithDisableDate_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var disableDate = new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate),
			Variations = new FlagVariations { DefaultVariation = "scheduled-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("scheduled-off");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}
}

public class ActivationScheduleEvaluator_ProcessEvaluation_Variations
{
	private readonly ActivationScheduleEvaluator _evaluator;

	public ActivationScheduleEvaluator_ProcessEvaluation_Variations()
	{
		_evaluator = new ActivationScheduleEvaluator();
	}

	[Fact]
	public async Task If_Enabled_ThenReturnsOnVariation()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, null),
			Variations = new FlagVariations { DefaultVariation = "custom-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_Disabled_ThenReturnsDefaultVariation()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, null),
			Variations = new FlagVariations { DefaultVariation = "custom-default" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("custom-default");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_DisabledByDisableDate_ThenReturnsDefaultVariation()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 25, 12, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var disableDate = new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate),
			Variations = new FlagVariations { DefaultVariation = "expired-variation" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("expired-variation");
		result.Reason.ShouldBe("Scheduled disable date passed");
	}
}

public class ActivationScheduleEvaluator_ProcessEvaluation_EdgeCases
{
	private readonly ActivationScheduleEvaluator _evaluator;

	public ActivationScheduleEvaluator_ProcessEvaluation_EdgeCases()
	{
		_evaluator = new ActivationScheduleEvaluator();
	}

	[Fact]
	public async Task If_EnableDateWithMillisecondsAfterEvaluationTime_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 10, 0, 0, 0, DateTimeKind.Utc);
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, 1, DateTimeKind.Utc); // 1ms later
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, null),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_DisableDateWithMillisecondsBeforeEvaluationTime_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 20, 10, 0, 0, 1, DateTimeKind.Utc); // 1ms after disable
		var enableDate = new DateTime(2024, 1, 15, 10, 0, 0, 0, DateTimeKind.Utc);
		var disableDate = new DateTime(2024, 1, 20, 10, 0, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, disableDate),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task If_OnlyDisableDateSet_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		var disableDate = new DateTime(2024, 1, 20, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(null, disableDate),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("The flag's activation schedule is not setup.");
	}
}

public class ActivationScheduleEvaluator_ProcessEvaluation_RealWorldScenarios
{
	private readonly ActivationScheduleEvaluator _evaluator;

	public ActivationScheduleEvaluator_ProcessEvaluation_RealWorldScenarios()
	{
		_evaluator = new ActivationScheduleEvaluator();
	}

	[Fact]
	public async Task FeatureLaunch_OneWeekWindow_ShouldWorkCorrectly()
	{
		// Arrange - Feature launches Monday 9 AM and ends the following Monday 9 AM
		var launchDate = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc); // Monday 9 AM
		var endDate = new DateTime(2024, 1, 22, 9, 0, 0, DateTimeKind.Utc);    // Next Monday 9 AM
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(launchDate, endDate),
			Variations = new FlagVariations { DefaultVariation = "feature-disabled" }
		};

		// Test before launch
		var beforeLaunch = launchDate.AddMinutes(-1);
		var contextBefore = new EvaluationContext(evaluationTime: beforeLaunch);
		var resultBefore = await _evaluator.ProcessEvaluation(flag, contextBefore);

		// Test during active period
		var duringActive = launchDate.AddDays(3);
		var contextDuring = new EvaluationContext(evaluationTime: duringActive);
		var resultDuring = await _evaluator.ProcessEvaluation(flag, contextDuring);

		// Test after end
		var afterEnd = endDate.AddMinutes(1);
		var contextAfter = new EvaluationContext(evaluationTime: afterEnd);
		var resultAfter = await _evaluator.ProcessEvaluation(flag, contextAfter);

		// Assert
		resultBefore.IsEnabled.ShouldBeFalse();
		resultBefore.Variation.ShouldBe("feature-disabled");
		resultBefore.Reason.ShouldBe("Scheduled enable date not reached");

		resultDuring.IsEnabled.ShouldBeTrue();
		resultDuring.Variation.ShouldBe("on");
		resultDuring.Reason.ShouldBe("Scheduled enable date reached");

		resultAfter.IsEnabled.ShouldBeFalse();
		resultAfter.Variation.ShouldBe("feature-disabled");
		resultAfter.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task MaintenanceWindow_ShortDuration_ShouldWorkCorrectly()
	{
		// Arrange - Maintenance window for 2 hours
		var startMaintenance = new DateTime(2024, 1, 15, 2, 0, 0, DateTimeKind.Utc);
		var endMaintenance = new DateTime(2024, 1, 15, 4, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(startMaintenance, endMaintenance),
			Variations = new FlagVariations { DefaultVariation = "service-unavailable" }
		};

		// Test just before maintenance
		var beforeMaintenance = startMaintenance.AddSeconds(-30);
		var resultBefore = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: beforeMaintenance));

		// Test during maintenance
		var duringMaintenance = startMaintenance.AddHours(1);
		var resultDuring = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: duringMaintenance));

		// Test just after maintenance
		var afterMaintenance = endMaintenance.AddSeconds(30);
		var resultAfter = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: afterMaintenance));

		// Assert
		resultBefore.IsEnabled.ShouldBeFalse();
		resultBefore.Reason.ShouldBe("Scheduled enable date not reached");

		resultDuring.IsEnabled.ShouldBeTrue();
		resultDuring.Reason.ShouldBe("Scheduled enable date reached");

		resultAfter.IsEnabled.ShouldBeFalse();
		resultAfter.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task PermanentFeature_NoDisableDate_ShouldStayEnabled()
	{
		// Arrange - Feature goes live and stays enabled
		var goLiveDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(goLiveDate, null),
			Variations = new FlagVariations { DefaultVariation = "beta-mode" }
		};

		// Test various times after go-live
		var testTimes = new[]
		{
			goLiveDate.AddMinutes(1),
			goLiveDate.AddDays(1),
			goLiveDate.AddMonths(1),
			goLiveDate.AddYears(1)
		};

		foreach (var testTime in testTimes)
		{
			// Act
			var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: testTime));

			// Assert
			result.IsEnabled.ShouldBeTrue();
			result.Variation.ShouldBe("on");
			result.Reason.ShouldBe("Scheduled enable date reached");
		}
	}
}

public class ActivationScheduleEvaluator_ProcessEvaluation_TimeZoneConsiderations
{
	private readonly ActivationScheduleEvaluator _evaluator;

	public ActivationScheduleEvaluator_ProcessEvaluation_TimeZoneConsiderations()
	{
		_evaluator = new ActivationScheduleEvaluator();
	}

	[Fact]
	public async Task If_ScheduleInUtc_EvaluationInUtc_ThenWorksCorrectly()
	{
		// Arrange
		var utcEnableDate = new DateTime(2024, 1, 15, 15, 0, 0, DateTimeKind.Utc); // 3 PM UTC
		var utcEvaluationTime = new DateTime(2024, 1, 15, 16, 0, 0, DateTimeKind.Utc); // 4 PM UTC
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(utcEnableDate, null),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: utcEvaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_UnspecifiedDateTimeKind_ThenEvaluatesCorrectly()
	{
		// Arrange - Real-world scenario where dates might come from different sources
		var enableDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Unspecified);
		var evaluationTime = new DateTime(2024, 1, 15, 13, 0, 0, DateTimeKind.Unspecified);
		
		var flag = new FeatureFlag
		{
			Schedule = FlagActivationSchedule.LoadSchedule(enableDate, null),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Scheduled enable date reached");
	}
}