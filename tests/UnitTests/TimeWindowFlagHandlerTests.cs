using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Client.Evaluators;

public class TimeWindowFlagHandler_CanProcessLogic
{
	private readonly TimeWindowFlagHandler _handler;

	public TimeWindowFlagHandler_CanProcessLogic()
	{
		_handler = new TimeWindowFlagHandler();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.Percentage)]
	public async Task If_FlagStatusNotTimeWindow_ThenCannotProcess(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "non-time-window-flag",
			Status = status,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain");
	}

	[Fact]
	public async Task If_FlagStatusIsTimeWindow_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "time-window-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			DefaultVariation = "default",
		};
		var noonTime = DateTime.Today.AddHours(12); // 12:00 PM
		var context = new EvaluationContext(evaluationTime: noonTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}
}

public class TimeWindowFlagHandler_ConfigurationValidation
{
	private readonly TimeWindowFlagHandler _handler;

	public TimeWindowFlagHandler_ConfigurationValidation()
	{
		_handler = new TimeWindowFlagHandler();
	}

	[Fact]
	public async Task If_NoWindowStartTime_ThenReturnsNotConfigured()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-start-time-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = null,
			WindowEndTime = TimeSpan.FromHours(17),
			DefaultVariation = "not-configured"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("not-configured");
		result.Reason.ShouldBe("Time window not configured");
	}

	[Fact]
	public async Task If_NoWindowEndTime_ThenReturnsNotConfigured()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-end-time-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = null,
			DefaultVariation = "not-configured"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("not-configured");
		result.Reason.ShouldBe("Time window not configured");
	}

	[Fact]
	public async Task If_BothWindowTimesNull_ThenReturnsNotConfigured()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-window-times-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = null,
			WindowEndTime = null,
			DefaultVariation = "not-configured"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("not-configured");
		result.Reason.ShouldBe("Time window not configured");
	}
}

public class TimeWindowFlagHandler_SameDayTimeWindow
{
	private readonly TimeWindowFlagHandler _handler;

	public TimeWindowFlagHandler_SameDayTimeWindow()
	{
		_handler = new TimeWindowFlagHandler();
	}

	[Theory]
	[InlineData(8, 59, false)] // Before window
	[InlineData(9, 0, true)]   // Start of window
	[InlineData(12, 30, true)] // Middle of window
	[InlineData(17, 0, true)]  // End of window
	[InlineData(17, 1, false)] // After window
	public async Task If_SameDayWindow_ThenEvaluatesTimeCorrectly(int hour, int minute, bool shouldBeEnabled)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "same-day-window-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),    // 9:00 AM
			WindowEndTime = TimeSpan.FromHours(17),     // 5:00 PM
			DefaultVariation = "outside-window"
		};
		var evaluationTime = DateTime.Today.AddHours(hour).AddMinutes(minute);
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBe(shouldBeEnabled);
		if (shouldBeEnabled)
		{
			result.Variation.ShouldBe("on");
			result.Reason.ShouldBe("Within time window");
		}
		else
		{
			result.Variation.ShouldBe("outside-window");
			result.Reason.ShouldBe("Outside time window");
		}
	}

	[Fact]
	public async Task If_ExactStartTime_ThenEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "exact-start-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(30)), // 10:30 AM
			WindowEndTime = TimeSpan.FromHours(15),                                  // 3:00 PM
			DefaultVariation = "default"
		};
		var evaluationTime = DateTime.Today.AddHours(10).AddMinutes(30); // Exactly 10:30 AM
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_ExactEndTime_ThenEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "exact-end-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17).Add(TimeSpan.FromMinutes(45)), // 5:45 PM
			DefaultVariation = "default"
		};
		var evaluationTime = DateTime.Today.AddHours(17).AddMinutes(45); // Exactly 5:45 PM
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}
}

public class TimeWindowFlagHandler_OvernightTimeWindow
{
	private readonly TimeWindowFlagHandler _handler;

	public TimeWindowFlagHandler_OvernightTimeWindow()
	{
		_handler = new TimeWindowFlagHandler();
	}

	[Theory]
	[InlineData(21, 59, false)] // Before window starts
	[InlineData(22, 0, true)]   // Start of window
	[InlineData(23, 30, true)]  // Late night
	[InlineData(2, 0, true)]    // Early morning
	[InlineData(6, 0, true)]    // End of window
	[InlineData(6, 1, false)]   // After window
	[InlineData(12, 0, false)]  // Middle of day (outside)
	public async Task If_OvernightWindow_ThenEvaluatesTimeCorrectly(int hour, int minute, bool shouldBeEnabled)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "overnight-window-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(22),   // 10:00 PM
			WindowEndTime = TimeSpan.FromHours(6),      // 6:00 AM
			DefaultVariation = "outside-overnight"
		};
		var evaluationTime = DateTime.Today.AddHours(hour).AddMinutes(minute);
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBe(shouldBeEnabled);
		if (shouldBeEnabled)
		{
			result.Variation.ShouldBe("on");
			result.Reason.ShouldBe("Within time window");
		}
		else
		{
			result.Variation.ShouldBe("outside-overnight");
			result.Reason.ShouldBe("Outside time window");
		}
	}

	[Fact]
	public async Task If_OvernightWindowMidnight_ThenEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "midnight-window-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(23),   // 11:00 PM
			WindowEndTime = TimeSpan.FromHours(1),      // 1:00 AM
			DefaultVariation = "default"
		};
		var evaluationTime = DateTime.Today.AddHours(0).AddMinutes(30); // 12:30 AM
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}
}

public class TimeWindowFlagHandler_DayOfWeekFiltering
{
	private readonly TimeWindowFlagHandler _handler;

	public TimeWindowFlagHandler_DayOfWeekFiltering()
	{
		_handler = new TimeWindowFlagHandler();
	}

	[Theory]
	[InlineData(DayOfWeek.Monday, true)]
	[InlineData(DayOfWeek.Tuesday, true)]
	[InlineData(DayOfWeek.Wednesday, true)]
	[InlineData(DayOfWeek.Thursday, true)]
	[InlineData(DayOfWeek.Friday, true)]
	[InlineData(DayOfWeek.Saturday, false)]
	[InlineData(DayOfWeek.Sunday, false)]
	public async Task If_WeekdaysOnly_ThenFiltersCorrectly(DayOfWeek dayOfWeek, bool shouldBeEnabled)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "weekdays-only-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			WindowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
			DefaultVariation = "weekend-disabled"
		};

		var baseDate = new DateTime(2023, 10, 1); // This is a Sunday, adjust to get desired day
		var daysToAdd = ((int)dayOfWeek - (int)baseDate.DayOfWeek + 7) % 7;
		var evaluationDate = baseDate.AddDays(daysToAdd);
		var evaluationTime = evaluationDate.AddHours(12); // 12:00 PM on the specified day
		
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBe(shouldBeEnabled);
		if (shouldBeEnabled)
		{
			result.Variation.ShouldBe("on");
			result.Reason.ShouldBe("Within time window");
		}
		else
		{
			result.Variation.ShouldBe("weekend-disabled");
			result.Reason.ShouldBe("Outside allowed days");
		}
	}

	[Fact]
	public async Task If_NoWindowDaysSpecified_ThenAllDaysAllowed()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "all-days-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			WindowDays = null, // No days specified
			DefaultVariation = "default"
		};
		var evaluationTime = DateTime.Today.AddHours(12); // 12:00 PM
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_EmptyWindowDaysList_ThenAllDaysAllowed()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "empty-days-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			WindowDays = new List<DayOfWeek>(), // Empty list
			DefaultVariation = "default"
		};
		var evaluationTime = DateTime.Today.AddHours(12); // 12:00 PM
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_WithinTimeButWrongDay_ThenDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "wrong-day-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			WindowDays = new List<DayOfWeek> { DayOfWeek.Monday },
			DefaultVariation = "wrong-day"
		};

		// Set to Tuesday at 12:00 PM (within time window but wrong day)
		var tuesday = new DateTime(2023, 10, 3, 12, 0, 0); // Oct 3, 2023 is a Tuesday
		var context = new EvaluationContext(evaluationTime: tuesday);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("wrong-day");
		result.Reason.ShouldBe("Outside allowed days");
	}

	[Fact]
	public async Task If_CorrectDayButOutsideTime_ThenDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "outside-time-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			WindowDays = new List<DayOfWeek> { DayOfWeek.Monday },
			DefaultVariation = "outside-time"
		};

		// Set to Monday at 8:00 AM (correct day but outside time window)
		var monday = new DateTime(2023, 10, 2, 8, 0, 0); // Oct 2, 2023 is a Monday
		var context = new EvaluationContext(evaluationTime: monday);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("outside-time");
		result.Reason.ShouldBe("Outside time window");
	}
}

public class TimeWindowFlagHandler_TimeZoneHandling
{
	private readonly TimeWindowFlagHandler _handler;

	public TimeWindowFlagHandler_TimeZoneHandling()
	{
		_handler = new TimeWindowFlagHandler();
	}

	[Fact]
	public async Task If_NoTimeZoneProvided_ThenUsesUTC()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "utc-default-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			TimeZone = null,
			DefaultVariation = "default"
		};
		
		// Set evaluation time to 12:00 PM UTC
		var utcTime = new DateTime(2023, 6, 15, 12, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: utcTime, timeZone: null);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_ContextTimeZoneProvided_ThenUsesContextTimeZone()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "context-timezone-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			TimeZone = "UTC",
			DefaultVariation = "default"
		};
		
		// Set evaluation time to 6:00 AM UTC (which would be 11:00 AM in Eastern Time)
		var utcTime = new DateTime(2023, 6, 15, 15, 0, 0, DateTimeKind.Utc); // 3:00 PM UTC
		var context = new EvaluationContext(evaluationTime: utcTime, timeZone: "Eastern Standard Time");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // 3:00 PM UTC = 11:00 AM EST, which is within 9-17 window
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_FlagTimeZoneProvided_ThenUsesFlagTimeZone()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "flag-timezone-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			TimeZone = "Pacific Standard Time",
			DefaultVariation = "default"
		};
		
		// Set evaluation time to 4:00 PM UTC (which would be 8:00 AM PST - outside window)
		var utcTime = new DateTime(2023, 1, 15, 16, 0, 0, DateTimeKind.Utc); // 4:00 PM UTC in January
		var context = new EvaluationContext(evaluationTime: utcTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // 5:00 PM UTC = 9:00 AM PST, which is within 9-17 window
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
		result.IsEnabled.ShouldBeTrue(); // 5:00 PM UTC = 9:00 AM PST, which is within 9-17 window
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
		}

	[Fact]
	public async Task If_ContextTimeZoneOverridesFlagTimeZone_ThenUsesContextTimeZone()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "timezone-override-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			TimeZone = "Pacific Standard Time", // Flag specifies PST
			DefaultVariation = "default"
		};
		
		var utcTime = new DateTime(2023, 1, 15, 14, 0, 0, DateTimeKind.Utc); // 2:00 PM UTC
		var context = new EvaluationContext(evaluationTime: utcTime, timeZone: "Eastern Standard Time"); // Context overrides with EST

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // 2:00 PM UTC = 9:00 AM EST, which is within window
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Theory]
	[InlineData("")]
	public async Task If_EmptyContextTimeZone_ThenUsesFlagOrDefaultTimeZone(string emptyTimeZone)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "empty-context-timezone-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			TimeZone = "Pacific Standard Time",
			DefaultVariation = "default"
		};
		
		var utcTime = new DateTime(2023, 1, 15, 17, 0, 0, DateTimeKind.Utc); // 5:00 PM UTC
		var context = new EvaluationContext(evaluationTime: utcTime, timeZone: emptyTimeZone);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // 5:00 PM UTC = 9:00 AM PST, which is within window
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}
}

public class TimeWindowFlagHandler_EvaluationTimeHandling
{
	private readonly TimeWindowFlagHandler _handler;

	public TimeWindowFlagHandler_EvaluationTimeHandling()
	{
		_handler = new TimeWindowFlagHandler();
	}

	[Fact]
	public async Task If_NoEvaluationTimeProvided_ThenUsesCurrentTime()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "current-time-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(0),   // All day window
			WindowEndTime = TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59)),
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: null); // No evaluation time provided

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // Should be enabled since it's an all-day window
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_EvaluationTimeProvided_ThenUsesProvidedTime()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "specific-time-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			DefaultVariation = "default"
		};
		
		var specificTime = new DateTime(2023, 6, 15, 8, 0, 0, DateTimeKind.Utc); // 8:00 AM - outside window
		var context = new EvaluationContext(evaluationTime: specificTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("Outside time window");
	}
}

public class TimeWindowFlagHandler_VariationHandling
{
	private readonly TimeWindowFlagHandler _handler;

	public TimeWindowFlagHandler_VariationHandling()
	{
		_handler = new TimeWindowFlagHandler();
	}

	[Fact]
	public async Task If_WithinTimeWindow_ThenReturnsOnVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "within-window-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			DefaultVariation = "should-not-be-used"
		};
		var evaluationTime = DateTime.Today.AddHours(12); // 12:00 PM
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Theory]
	[InlineData("outside-hours")]
	[InlineData("closed")]
	[InlineData("default")]
	[InlineData("")]
	public async Task If_OutsideTimeWindow_ThenReturnsDefaultVariation(string defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "outside-window-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			DefaultVariation = defaultVariation
		};
		var evaluationTime = DateTime.Today.AddHours(8); // 8:00 AM - outside window
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
		result.Reason.ShouldBe("Outside time window");
	}

	[Fact]
	public async Task If_OutsideAllowedDays_ThenReturnsDefaultVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "outside-days-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			WindowDays = new List<DayOfWeek> { DayOfWeek.Monday },
			DefaultVariation = "weekends-disabled"
		};
		
		// Set to Sunday at 12:00 PM (within time but wrong day)
		var sunday = new DateTime(2023, 10, 1, 12, 0, 0); // Oct 1, 2023 is a Sunday
		var context = new EvaluationContext(evaluationTime: sunday);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("weekends-disabled");
		result.Reason.ShouldBe("Outside allowed days");
	}

	[Fact]
	public async Task If_TimeWindowNotConfigured_ThenReturnsDefaultVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-configured-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = null,
			WindowEndTime = TimeSpan.FromHours(17),
			DefaultVariation = "configuration-error"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("configuration-error");
		result.Reason.ShouldBe("Time window not configured");
	}

	[Theory]
	[InlineData(null)]
	public async Task If_NullDefaultVariation_ThenReturnsNull(string? defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "null-variation-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			DefaultVariation = defaultVariation!
		};
		var evaluationTime = DateTime.Today.AddHours(8); // Outside window
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Outside time window");
	}
}

public class TimeWindowFlagHandler_ChainOfResponsibilityIntegration
{
	private readonly TimeWindowFlagHandler _handler;
	private readonly Mock<IFlagEvaluationHandler> _mockNextHandler;

	public TimeWindowFlagHandler_ChainOfResponsibilityIntegration()
	{
		_handler = new TimeWindowFlagHandler();
		_mockNextHandler = new Mock<IFlagEvaluationHandler>();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.Percentage)]
	public async Task If_FlagStatusNotTimeWindow_ThenCallsNextHandler(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "non-time-window-flag",
			Status = status,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext();
		var expectedResult = new EvaluationResult(isEnabled: true, variation: "handled-by-next");

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
	public async Task If_FlagStatusTimeWindow_ThenDoesNotCallNextHandler()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "time-window-flag",
			Status = FeatureFlagStatus.TimeWindow,
			WindowStartTime = TimeSpan.FromHours(9),
			WindowEndTime = TimeSpan.FromHours(17),
			DefaultVariation = "default"
		};
		var evaluationTime = DateTime.Today.AddHours(12); // Within window
		var context = new EvaluationContext(evaluationTime: evaluationTime);

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
			Status = FeatureFlagStatus.Enabled, // Not time window
			DefaultVariation = "default"
		};
		var context = new EvaluationContext();

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