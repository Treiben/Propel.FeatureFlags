using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace FeatureFlags.UnitTests.Evaluation.Handlers;

public class OperationalWindowEvaluator_EvaluationOrder
{
	[Fact]
	public void EvaluationOrder_ShouldReturnOperationalWindow()
	{
		// Arrange
		var evaluator = new OperationalWindowEvaluator();

		// Act
		var order = evaluator.EvaluationOrder;

		// Assert
		order.ShouldBe(EvaluationOrder.OperationalWindow);
	}
}

public class OperationalWindowEvaluator_CanProcess
{
	private readonly OperationalWindowEvaluator _evaluator;

	public OperationalWindowEvaluator_CanProcess()
	{
		_evaluator = new OperationalWindowEvaluator();
	}

	[Fact]
	public void If_FlagContainsTimeWindowMode_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_FlagContainsMultipleModesIncludingTimeWindow_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData(FlagEvaluationMode.Disabled)]
	[InlineData(FlagEvaluationMode.Enabled)]
	[InlineData(FlagEvaluationMode.Scheduled)]
	[InlineData(FlagEvaluationMode.UserTargeted)]
	[InlineData(FlagEvaluationMode.UserRolloutPercentage)]
	public void If_FlagDoesNotContainTimeWindowMode_ThenCannotProcess(FlagEvaluationMode mode)
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

public class OperationalWindowEvaluator_ProcessEvaluation_WindowValidation
{
	private readonly OperationalWindowEvaluator _evaluator;

	public OperationalWindowEvaluator_ProcessEvaluation_WindowValidation()
	{
		_evaluator = new OperationalWindowEvaluator();
	}

	[Fact]
	public async Task If_WindowIsAlwaysOpen_ThenDoesNotThrow()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.AlwaysOpen,
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
	}
}

public class OperationalWindowEvaluator_ProcessEvaluation_EvaluationTime
{
	private readonly OperationalWindowEvaluator _evaluator;

	public OperationalWindowEvaluator_ProcessEvaluation_EvaluationTime()
	{
		_evaluator = new OperationalWindowEvaluator();
	}

	[Fact]
	public async Task If_EvaluationTimeProvided_ThenUsesProvidedTime()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // Monday 12 PM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17)),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_EvaluationTimeNotProvided_ThenUsesCurrentTime()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.AlwaysOpen,
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: null);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Flag operational window is always open.");
	}
}

public class OperationalWindowEvaluator_ProcessEvaluation_TimeWindowLogic
{
	private readonly OperationalWindowEvaluator _evaluator;

	public OperationalWindowEvaluator_ProcessEvaluation_TimeWindowLogic()
	{
		_evaluator = new OperationalWindowEvaluator();
	}

	[Fact]
	public async Task If_WithinSameDayWindow_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // Monday 12 PM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),   // 9 AM
				TimeSpan.FromHours(17)), // 5 PM
			Variations = new FlagVariations { DefaultVariation = "window-closed" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_OutsideSameDayWindow_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc); // Monday 8 PM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),   // 9 AM
				TimeSpan.FromHours(17)), // 5 PM
			Variations = new FlagVariations { DefaultVariation = "window-closed" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("window-closed");
		result.Reason.ShouldBe("Outside time window");
	}

	[Fact]
	public async Task If_WithinOvernightWindow_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 2, 0, 0, DateTimeKind.Utc); // Monday 2 AM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(22), // 10 PM
				TimeSpan.FromHours(6)), // 6 AM
			Variations = new FlagVariations { DefaultVariation = "maintenance-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_OutsideOvernightWindow_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // Monday 12 PM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(22), // 10 PM
				TimeSpan.FromHours(6)), // 6 AM
			Variations = new FlagVariations { DefaultVariation = "maintenance-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("maintenance-off");
		result.Reason.ShouldBe("Outside time window");
	}

	[Fact]
	public async Task If_ExactlyAtStartTime_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 9, 30, 0, DateTimeKind.Utc); // Monday 9:30 AM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9).Add(TimeSpan.FromMinutes(30)), // 9:30 AM
				TimeSpan.FromHours(17)),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_ExactlyAtEndTime_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 17, 30, 0, DateTimeKind.Utc); // Monday 5:30 PM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17).Add(TimeSpan.FromMinutes(30))), // 5:30 PM
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}
}

public class OperationalWindowEvaluator_ProcessEvaluation_DayOfWeekLogic
{
	private readonly OperationalWindowEvaluator _evaluator;

	public OperationalWindowEvaluator_ProcessEvaluation_DayOfWeekLogic()
	{
		_evaluator = new OperationalWindowEvaluator();
	}

	[Fact]
	public async Task If_WithinTimeWindowButWrongDay_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 17, 12, 0, 0, DateTimeKind.Utc); // Wednesday 12 PM UTC
		var weekdaysOnly = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday };
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				allowedDays: weekdaysOnly),
			Variations = new FlagVariations { DefaultVariation = "weekdays-only" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("weekdays-only");
		result.Reason.ShouldBe("Outside allowed days");
	}

	[Fact]
	public async Task If_OutsideTimeWindowButCorrectDay_ThenReturnsDisabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 8, 0, 0, DateTimeKind.Utc); // Monday 8 AM UTC (outside window)
		var mondaysOnly = new List<DayOfWeek> { DayOfWeek.Monday };
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				allowedDays: mondaysOnly),
			Variations = new FlagVariations { DefaultVariation = "mondays-only" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("mondays-only");
		result.Reason.ShouldBe("Outside time window");
	}

	[Fact]
	public async Task If_WithinTimeWindowAndCorrectDay_ThenReturnsEnabled()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // Monday 12 PM UTC
		var mondaysOnly = new List<DayOfWeek> { DayOfWeek.Monday };
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				allowedDays: mondaysOnly),
			Variations = new FlagVariations { DefaultVariation = "mondays-only" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Theory]
	[InlineData(DayOfWeek.Monday, "2024-01-15")]
	[InlineData(DayOfWeek.Tuesday, "2024-01-16")]
	[InlineData(DayOfWeek.Wednesday, "2024-01-17")]
	[InlineData(DayOfWeek.Thursday, "2024-01-18")]
	[InlineData(DayOfWeek.Friday, "2024-01-19")]
	public async Task If_WeekdayWindow_ThenCorrectDaysEnabled(DayOfWeek expectedDay, string dateString)
	{
		// Arrange
		var evaluationTime = DateTime.Parse(dateString + "T12:00:00Z"); // 12 PM on the specified day
		var weekdaysOnly = new List<DayOfWeek> 
		{ 
			DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
			DayOfWeek.Thursday, DayOfWeek.Friday 
		};
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				allowedDays: weekdaysOnly),
			Variations = new FlagVariations { DefaultVariation = "weekend-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);
		var evaluator = new OperationalWindowEvaluator();

		// Act
		var result = await evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
		evaluationTime.DayOfWeek.ShouldBe(expectedDay);
	}

	[Theory]
	[InlineData(DayOfWeek.Saturday, "2024-01-20")]
	[InlineData(DayOfWeek.Sunday, "2024-01-21")]
	public async Task If_WeekdayWindow_ThenWeekendsDisabled(DayOfWeek expectedDay, string dateString)
	{
		// Arrange
		var evaluationTime = DateTime.Parse(dateString + "T12:00:00Z"); // 12 PM on the specified day
		var weekdaysOnly = new List<DayOfWeek> 
		{ 
			DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
			DayOfWeek.Thursday, DayOfWeek.Friday 
		};
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				allowedDays: weekdaysOnly),
			Variations = new FlagVariations { DefaultVariation = "weekend-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);
		var evaluator = new OperationalWindowEvaluator();

		// Act
		var result = await evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("weekend-off");
		result.Reason.ShouldBe("Outside allowed days");
		evaluationTime.DayOfWeek.ShouldBe(expectedDay);
	}
}

public class OperationalWindowEvaluator_ProcessEvaluation_TimeZoneHandling
{
	private readonly OperationalWindowEvaluator _evaluator;

	public OperationalWindowEvaluator_ProcessEvaluation_TimeZoneHandling()
	{
		_evaluator = new OperationalWindowEvaluator();
	}

	[Fact]
	public async Task If_ContextTimeZoneProvided_ThenUsesContextTimeZone()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc); // 5 PM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				"Pacific Standard Time"), // Window is in PST
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(
			evaluationTime: evaluationTime,
			timeZone: "Eastern Standard Time"); // Context requests EST

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert - 5 PM UTC = 12 PM EST, should be within 9-17 window
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_NoContextTimeZone_ThenUsesWindowTimeZone()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc); // 5 PM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				"Pacific Standard Time"), // Window is in PST
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(
			evaluationTime: evaluationTime,
			timeZone: null); // No context timezone

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert - 5 PM UTC = 9 AM PST in January, should be within 9-17 window
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_InvalidTimeZone_ThenReturnsDisabledWithError()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17)),
			Variations = new FlagVariations { DefaultVariation = "timezone-error" }
		};
		var context = new EvaluationContext(
			evaluationTime: evaluationTime,
			timeZone: "Invalid/TimeZone");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("timezone-error");
		result.Reason.ShouldBe("Invalid timezone: Invalid/TimeZone");
	}

	[Fact]
	public async Task If_TimeZoneConversionCrossesDay_ThenUsesCorrectDay()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 6, 0, 0, DateTimeKind.Utc); // 6 AM UTC on Monday
		var mondayOnly = new List<DayOfWeek> { DayOfWeek.Monday };
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(20), // 8 PM
				TimeSpan.FromHours(23), // 11 PM
				allowedDays: mondayOnly),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(
			evaluationTime: evaluationTime,
			timeZone: "Pacific Standard Time"); // 6 AM UTC = 10 PM PST on Sunday

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert - Should be Sunday in PST, not Monday
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Outside allowed days");
	}
}

public class OperationalWindowEvaluator_ProcessEvaluation_Variations
{
	private readonly OperationalWindowEvaluator _evaluator;

	public OperationalWindowEvaluator_ProcessEvaluation_Variations()
	{
		_evaluator = new OperationalWindowEvaluator();
	}

	[Fact]
	public async Task If_Enabled_ThenReturnsOnVariation()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // Monday 12 PM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17)),
			Variations = new FlagVariations { DefaultVariation = "custom-off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_Disabled_ThenReturnsDefaultVariation()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc); // Monday 8 PM UTC (outside window)
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17)),
			Variations = new FlagVariations { DefaultVariation = "after-hours" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("after-hours");
		result.Reason.ShouldBe("Outside time window");
	}

	[Fact]
	public async Task If_DisabledByDay_ThenReturnsDefaultVariation()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Utc); // Saturday 12 PM UTC
		var weekdaysOnly = new List<DayOfWeek> 
		{ 
			DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
			DayOfWeek.Thursday, DayOfWeek.Friday 
		};
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				allowedDays: weekdaysOnly),
			Variations = new FlagVariations { DefaultVariation = "weekend-mode" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("weekend-mode");
		result.Reason.ShouldBe("Outside allowed days");
	}
}

public class OperationalWindowEvaluator_ProcessEvaluation_EdgeCases
{
	private readonly OperationalWindowEvaluator _evaluator;

	public OperationalWindowEvaluator_ProcessEvaluation_EdgeCases()
	{
		_evaluator = new OperationalWindowEvaluator();
	}

	[Fact]
	public async Task If_MidnightWindow_ThenWorksCorrectly()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 3, 0, 0, DateTimeKind.Utc); // Monday 3 AM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.Zero,           // Midnight
				TimeSpan.FromHours(6)),  // 6 AM
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_AlmostMidnightWindow_ThenWorksCorrectly()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 23, 30, 0, DateTimeKind.Utc); // Monday 11:30 PM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(20),           // 8 PM
				new TimeSpan(23, 59, 59)),        // 23:59:59
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_SingleSecondWindow_ThenWorksCorrectly()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // Monday exactly 12:00:00 PM UTC
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(12),                         // 12:00:00 PM
				TimeSpan.FromHours(12).Add(TimeSpan.FromSeconds(1))), // 12:00:01 PM
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_SingleDayWindow_ThenWorksCorrectly()
	{
		// Arrange
		var evaluationTime = new DateTime(2024, 1, 17, 12, 0, 0, DateTimeKind.Utc); // Wednesday 12 PM UTC
		var wednesdayOnly = new List<DayOfWeek> { DayOfWeek.Wednesday };
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				allowedDays: wednesdayOnly),
			Variations = new FlagVariations { DefaultVariation = "other-days" }
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_NonUtcEvaluationTime_ThenHandledCorrectly()
	{
		// Arrange
		var localTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Local);
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.AlwaysOpen,
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(evaluationTime: localTime);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // AlwaysOpen should always be enabled
		result.Reason.ShouldBe("Flag operational window is always open.");
	}
}

public class OperationalWindowEvaluator_ProcessEvaluation_RealWorldScenarios
{
	private readonly OperationalWindowEvaluator _evaluator;

	public OperationalWindowEvaluator_ProcessEvaluation_RealWorldScenarios()
	{
		_evaluator = new OperationalWindowEvaluator();
	}

	[Fact]
	public async Task BusinessHours_WeekdaysOnly_ShouldWorkCorrectly()
	{
		// Arrange - Business hours: 9 AM - 5 PM, Monday to Friday
		var businessDays = new List<DayOfWeek> 
		{ 
			DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
			DayOfWeek.Thursday, DayOfWeek.Friday 
		};
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(17),
				allowedDays: businessDays),
			Variations = new FlagVariations { DefaultVariation = "after-hours" }
		};

		// Test during business hours (Wednesday 2 PM)
		var businessHours = new DateTime(2024, 1, 17, 14, 0, 0, DateTimeKind.Utc);
		var resultBusiness = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: businessHours));

		// Test after hours (Wednesday 8 PM)
		var afterHours = new DateTime(2024, 1, 17, 20, 0, 0, DateTimeKind.Utc);
		var resultAfter = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: afterHours));

		// Test weekend (Saturday 2 PM)
		var weekend = new DateTime(2024, 1, 20, 14, 0, 0, DateTimeKind.Utc);
		var resultWeekend = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: weekend));

		// Assert
		resultBusiness.IsEnabled.ShouldBeTrue();
		resultBusiness.Reason.ShouldBe("Within time window");

		resultAfter.IsEnabled.ShouldBeFalse();
		resultAfter.Variation.ShouldBe("after-hours");
		resultAfter.Reason.ShouldBe("Outside time window");

		resultWeekend.IsEnabled.ShouldBeFalse();
		resultWeekend.Variation.ShouldBe("after-hours");
		resultWeekend.Reason.ShouldBe("Outside allowed days");
	}

	[Fact]
	public async Task MaintenanceWindow_OvernightWeekends_ShouldWorkCorrectly()
	{
		// Arrange - Maintenance window: 10 PM - 6 AM on weekends only
		var weekendDays = new List<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(22), // 10 PM
				TimeSpan.FromHours(6),  // 6 AM
				allowedDays: weekendDays),
			Variations = new FlagVariations { DefaultVariation = "maintenance-off" }
		};

		// Test during maintenance window (Sunday 2 AM)
		var maintenanceTime = new DateTime(2024, 1, 21, 2, 0, 0, DateTimeKind.Utc);
		var resultMaintenance = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: maintenanceTime));

		// Test weekend day but outside maintenance hours (Saturday 3 PM)
		var weekendDay = new DateTime(2024, 1, 20, 15, 0, 0, DateTimeKind.Utc);
		var resultWeekendDay = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: weekendDay));

		// Test weekday during maintenance hours (Tuesday 2 AM)
		var weekdayNight = new DateTime(2024, 1, 16, 2, 0, 0, DateTimeKind.Utc);
		var resultWeekdayNight = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: weekdayNight));

		// Assert
		resultMaintenance.IsEnabled.ShouldBeTrue();
		resultMaintenance.Reason.ShouldBe("Within time window");

		resultWeekendDay.IsEnabled.ShouldBeFalse();
		resultWeekendDay.Reason.ShouldBe("Outside time window");

		resultWeekdayNight.IsEnabled.ShouldBeFalse();
		resultWeekdayNight.Reason.ShouldBe("Outside allowed days");
	}

	[Fact]
	public async Task GlobalFeature_MultipleTimeZones_ShouldWorkCorrectly()
	{
		// Arrange - Feature available 9 AM - 9 PM in user's local timezone
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.FromHours(9),
				TimeSpan.FromHours(21),  // 9 PM
				"UTC"), // Window defined in UTC
			Variations = new FlagVariations { DefaultVariation = "outside-hours" }
		};

		var utcNoon = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc); // 12 PM UTC

		// Test with Eastern time context (12 PM UTC = 7 AM EST - outside window)
		var resultEastern = await _evaluator.ProcessEvaluation(flag, 
			new EvaluationContext(evaluationTime: utcNoon, timeZone: "Eastern Standard Time"));

		// Test with Pacific time context (12 PM UTC = 4 AM PST - outside window)
		var resultPacific = await _evaluator.ProcessEvaluation(flag, 
			new EvaluationContext(evaluationTime: utcNoon, timeZone: "Pacific Standard Time"));

		// Test with UTC context (12 PM UTC = within window)
		var resultUtc = await _evaluator.ProcessEvaluation(flag, 
			new EvaluationContext(evaluationTime: utcNoon, timeZone: "UTC"));

		// Assert
		resultEastern.IsEnabled.ShouldBeFalse();
		resultEastern.Reason.ShouldBe("Outside time window");

		resultPacific.IsEnabled.ShouldBeFalse();
		resultPacific.Reason.ShouldBe("Outside time window");

		resultUtc.IsEnabled.ShouldBeTrue();
		resultUtc.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task LimitedFeature_OnlyTuesdays_ShouldWorkCorrectly()
	{
		// Arrange - Feature only available on Tuesdays, all day
		var tuesdayOnly = new List<DayOfWeek> { DayOfWeek.Tuesday };
		
		var flag = new FeatureFlag
		{
			OperationalWindow = FlagOperationalWindow.CreateWindow(
				TimeSpan.Zero,
				new TimeSpan(23, 59, 59),
				allowedDays: tuesdayOnly),
			Variations = new FlagVariations { DefaultVariation = "tuesday-special-off" }
		};

		var testDays = new[]
		{
			(DayOfWeek.Monday, new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc), false),
			(DayOfWeek.Tuesday, new DateTime(2024, 1, 16, 12, 0, 0, DateTimeKind.Utc), true),
			(DayOfWeek.Wednesday, new DateTime(2024, 1, 17, 12, 0, 0, DateTimeKind.Utc), false),
			(DayOfWeek.Thursday, new DateTime(2024, 1, 18, 12, 0, 0, DateTimeKind.Utc), false),
			(DayOfWeek.Friday, new DateTime(2024, 1, 19, 12, 0, 0, DateTimeKind.Utc), false),
			(DayOfWeek.Saturday, new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Utc), false),
			(DayOfWeek.Sunday, new DateTime(2024, 1, 21, 12, 0, 0, DateTimeKind.Utc), false)
		};

		foreach (var (expectedDay, testTime, shouldBeEnabled) in testDays)
		{
			// Act
			var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(evaluationTime: testTime));

			// Assert
			testTime.DayOfWeek.ShouldBe(expectedDay);
			result.IsEnabled.ShouldBe(shouldBeEnabled);
			
			if (shouldBeEnabled)
			{
				result.Variation.ShouldBe("on");
				result.Reason.ShouldBe("Within time window");
			}
			else
			{
				result.Variation.ShouldBe("tuesday-special-off");
				result.Reason.ShouldBe("Outside allowed days");
			}
		}
	}
}