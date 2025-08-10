using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Client.Evaluators;

public class ScheduledFlagHandler_CanProcessLogic
{
	private readonly ScheduledFlagHandler _handler;

	public ScheduledFlagHandler_CanProcessLogic()
	{
		_handler = new ScheduledFlagHandler();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.Percentage)]
	public async Task If_FlagStatusNotScheduled_ThenCannotProcess(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			Status = status,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: DateTime.UtcNow);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain");
	}

	[Fact]
	public async Task If_FlagStatusIsScheduled_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = DateTime.UtcNow.AddHours(-1), // Already enabled
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: DateTime.UtcNow);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldContain("Scheduled enable date reached");
	}
}

public class ScheduledFlagHandler_EnableDateLogic
{
	private readonly ScheduledFlagHandler _handler;

	public ScheduledFlagHandler_EnableDateLogic()
	{
		_handler = new ScheduledFlagHandler();
	}

	[Fact]
	public async Task If_NoScheduledEnableDate_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-enable-date-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = null,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: DateTime.UtcNow);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_EnableDateInFuture_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "future-enable-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime.AddHours(1), // Future date
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_EnableDateReached_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "enabled-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime, // Exact time
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_EnableDateInPast_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "past-enable-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime.AddHours(-2), // Past date
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}
}

public class ScheduledFlagHandler_DisableDateLogic
{
	private readonly ScheduledFlagHandler _handler;

	public ScheduledFlagHandler_DisableDateLogic()
	{
		_handler = new ScheduledFlagHandler();
	}

	[Fact]
	public async Task If_NoDisableDateAndEnabled_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "no-disable-date-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime.AddHours(-1), // Already enabled
			ScheduledDisableDate = null, // No disable date
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_DisableDateInFutureAndEnabled_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "future-disable-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime.AddHours(-1), // Already enabled
			ScheduledDisableDate = evaluationTime.AddHours(1), // Future disable
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_DisableDateReached_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "disable-reached-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime.AddHours(-2), // Already enabled
			ScheduledDisableDate = evaluationTime, // Disable time reached
			DefaultVariation = "disabled-variation"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-variation");
		result.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task If_DisableDateInPast_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "past-disable-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime.AddHours(-3), // Already enabled
			ScheduledDisableDate = evaluationTime.AddHours(-1), // Already disabled
			DefaultVariation = "expired-variation"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("expired-variation");
		result.Reason.ShouldBe("Scheduled disable date passed");
	}
}

public class ScheduledFlagHandler_ScheduleLifecycle
{
	private readonly ScheduledFlagHandler _handler;

	public ScheduledFlagHandler_ScheduleLifecycle()
	{
		_handler = new ScheduledFlagHandler();
	}

	[Fact]
	public async Task If_BeforeEnableDate_ThenDisabled()
	{
		// Arrange
		var baseTime = new DateTime(2023, 6, 15, 12, 0, 0, DateTimeKind.Utc);
		var flag = new FeatureFlag
		{
			Key = "lifecycle-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = baseTime.AddHours(1), // Enable at 13:00
			ScheduledDisableDate = baseTime.AddHours(3), // Disable at 15:00
			DefaultVariation = "before-enable"
		};
		var context = new EvaluationContext(evaluationTime: baseTime); // Current time 12:00

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("before-enable");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_DuringEnabledPeriod_ThenEnabled()
	{
		// Arrange
		var baseTime = new DateTime(2023, 6, 15, 12, 0, 0, DateTimeKind.Utc);
		var flag = new FeatureFlag
		{
			Key = "lifecycle-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = baseTime.AddHours(-1), // Enabled at 11:00
			ScheduledDisableDate = baseTime.AddHours(1), // Disable at 13:00
			DefaultVariation = "during-period"
		};
		var context = new EvaluationContext(evaluationTime: baseTime); // Current time 12:00

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_AfterDisableDate_ThenDisabled()
	{
		// Arrange
		var baseTime = new DateTime(2023, 6, 15, 12, 0, 0, DateTimeKind.Utc);
		var flag = new FeatureFlag
		{
			Key = "lifecycle-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = baseTime.AddHours(-3), // Enabled at 09:00
			ScheduledDisableDate = baseTime.AddHours(-1), // Disabled at 11:00
			DefaultVariation = "after-disable"
		};
		var context = new EvaluationContext(evaluationTime: baseTime); // Current time 12:00

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("after-disable");
		result.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Theory]
	[InlineData(-60, -30, -45)] // All in past: enabled 1hr ago, disabled 30min ago, eval 45min ago
	[InlineData(30, 60, 45)] // All in future: enable in 30min, disable in 1hr, eval in 45min
	[InlineData(-30, 30, 0)] // Currently enabled: enabled 30min ago, disable in 30min, eval now
	public async Task If_VariousTimeScenarios_ThenEvaluatesCorrectly(int enableMinutesOffset, int disableMinutesOffset, int evalMinutesOffset)
	{
		// Arrange
		var baseTime = new DateTime(2023, 6, 15, 12, 0, 0, DateTimeKind.Utc);
		var evaluationTime = baseTime.AddMinutes(evalMinutesOffset);
		var flag = new FeatureFlag
		{
			Key = "time-scenario-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = baseTime.AddMinutes(enableMinutesOffset),
			ScheduledDisableDate = baseTime.AddMinutes(disableMinutesOffset),
			DefaultVariation = "scenario-default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		
		var enableReached = evaluationTime >= flag.ScheduledEnableDate.Value;
		var disableReached = evaluationTime >= flag.ScheduledDisableDate.Value;

		if (!enableReached)
		{
			result.IsEnabled.ShouldBeFalse();
			result.Variation.ShouldBe("scenario-default");
			result.Reason.ShouldBe("Scheduled enable date not reached");
		}
		else if (disableReached)
		{
			result.IsEnabled.ShouldBeFalse();
			result.Variation.ShouldBe("scenario-default");
			result.Reason.ShouldBe("Scheduled disable date passed");
		}
		else
		{
			result.IsEnabled.ShouldBeTrue();
			result.Variation.ShouldBe("on");
			result.Reason.ShouldBe("Scheduled enable date reached");
		}
	}
}

public class ScheduledFlagHandler_EvaluationTimeHandling
{
	private readonly ScheduledFlagHandler _handler;

	public ScheduledFlagHandler_EvaluationTimeHandling()
	{
		_handler = new ScheduledFlagHandler();
	}

	[Fact]
	public async Task If_NoEvaluationTimeProvided_ThenUsesCurrentTime()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "current-time-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = DateTime.UtcNow.AddMinutes(-5), // Should be enabled
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: null); // No evaluation time

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_EvaluationTimeProvided_ThenUsesProvidedTime()
	{
		// Arrange
		var specificTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var flag = new FeatureFlag
		{
			Key = "specific-time-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = specificTime.AddHours(1), // Enable 1 hour after specific time
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: specificTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}
}

public class ScheduledFlagHandler_VariationHandling
{
	private readonly ScheduledFlagHandler _handler;

	public ScheduledFlagHandler_VariationHandling()
	{
		_handler = new ScheduledFlagHandler();
	}

	[Theory]
	[InlineData("control")]
	[InlineData("off")]
	[InlineData("disabled")]
	[InlineData("fallback")]
	[InlineData("")]
	public async Task If_FlagDisabled_ThenReturnsDefaultVariation(string defaultVariation)
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "variation-test-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime.AddHours(1), // Not yet enabled
			DefaultVariation = defaultVariation
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_FlagEnabled_ThenReturnsOnVariation()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "enabled-variation-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime.AddHours(-1), // Already enabled
			DefaultVariation = "should-not-be-used"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_FlagDisabledAfterSchedule_ThenReturnsDefaultVariation()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "disabled-after-schedule-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime.AddHours(-2), // Was enabled
			ScheduledDisableDate = evaluationTime.AddHours(-1), // Now disabled
			DefaultVariation = "post-schedule-default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("post-schedule-default");
		result.Reason.ShouldBe("Scheduled disable date passed");
	}

	[Fact]
	public async Task If_NullDefaultVariation_ThenReturnsNull()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "null-variation-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = evaluationTime.AddHours(1), // Not yet enabled
			DefaultVariation = null!
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}
}

public class ScheduledFlagHandler_ChainOfResponsibilityIntegration
{
	private readonly ScheduledFlagHandler _handler;
	private readonly Mock<IFlagEvaluationHandler> _mockNextHandler;

	public ScheduledFlagHandler_ChainOfResponsibilityIntegration()
	{
		_handler = new ScheduledFlagHandler();
		_mockNextHandler = new Mock<IFlagEvaluationHandler>();
	}

	[Fact]
	public async Task If_FlagNotScheduled_ThenCallsNextHandler()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-scheduled-flag",
			Status = FeatureFlagStatus.Enabled, // Not scheduled
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: DateTime.UtcNow);
		var expectedResult = new EvaluationResult(isEnabled: true, variation: "enabled-by-next");

		_mockNextHandler.Setup(x => x.Handle(flag, context))
			.ReturnsAsync(expectedResult);
		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldBe(expectedResult);
		_mockNextHandler.Verify(x => x.Handle(flag, context), Times.Once);
	}

	[Fact]
	public async Task If_FlagScheduled_ThenDoesNotCallNextHandler()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "scheduled-flag",
			Status = FeatureFlagStatus.Scheduled,
			ScheduledEnableDate = DateTime.UtcNow.AddHours(-1), // Already enabled
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: DateTime.UtcNow);

		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		_mockNextHandler.Verify(x => x.Handle(It.IsAny<FeatureFlag>(), It.IsAny<EvaluationContext>()), Times.Never);
	}

	[Fact]
	public async Task If_NoNextHandler_ThenReturnsAppropriateResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-next-handler-flag",
			Status = FeatureFlagStatus.Enabled, // Not scheduled
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: DateTime.UtcNow);

		// NextHandler is null by default

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldContain("End of evaluation chain");
	}
}