using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Client.Evaluators;

public class ExpirationDateHandler_CanProcessLogic
{
	private readonly ExpirationDateHandler _handler;

	public ExpirationDateHandler_CanProcessLogic()
	{
		_handler = new ExpirationDateHandler();
	}

	[Fact]
	public async Task If_FlagHasNoExpirationDate_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			ExpirationDate = null,
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
	public async Task If_FlagHasDefaultExpirationDate_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			ExpirationDate = default(DateTime),
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
	public async Task If_FlagNotExpired_ThenCannotProcess()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			ExpirationDate = evaluationTime.AddHours(1), // Expires in the future
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain");
	}

	[Fact]
	public async Task If_FlagExpired_ThenCanProcess()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			ExpirationDate = evaluationTime.AddHours(-1), // Expired in the past
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldContain("Flag test-flag expired at");
	}
}

public class ExpirationDateHandler_EvaluationTimeHandling
{
	private readonly ExpirationDateHandler _handler;

	public ExpirationDateHandler_EvaluationTimeHandling()
	{
		_handler = new ExpirationDateHandler();
	}

	[Fact]
	public async Task If_NoEvaluationTimeProvided_ThenUsesCurrentTime()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			ExpirationDate = DateTime.UtcNow.AddHours(-1), // Expired in the past
			DefaultVariation = "expired-variation"
		};
		var context = new EvaluationContext(evaluationTime: null); // No evaluation time

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("expired-variation");
		result.Reason.ShouldContain("Flag test-flag expired at");
	}

	[Fact]
	public async Task If_EvaluationTimeProvided_ThenUsesProvidedTime()
	{
		// Arrange
		var specificTime = new DateTime(2023, 6, 15, 10, 30, 0, DateTimeKind.Utc);
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			ExpirationDate = specificTime.AddHours(-2), // Expired 2 hours before specific time
			DefaultVariation = "expired-variation"
		};
		var context = new EvaluationContext(evaluationTime: specificTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("expired-variation");
		result.Reason.ShouldContain("current time 6/15/2023 10:30:00 AM");
	}

	[Theory]
	[InlineData(-1)] // 1 minute past expiration
	[InlineData(-60)] // 1 hour past expiration
	[InlineData(-1440)] // 1 day past expiration
	public async Task If_FlagExpiredByVariousAmounts_ThenProcessesCorrectly(int minutesAfterExpiration)
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var expirationTime = evaluationTime.AddMinutes(minutesAfterExpiration);
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			ExpirationDate = expirationTime,
			DefaultVariation = "expired"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("expired");
		result.Reason.ShouldContain($"Flag test-flag expired at {expirationTime}");
	}

	[Theory]
	[InlineData(1)] // 1 minute before expiration
	[InlineData(60)] // 1 hour before expiration
	[InlineData(1440)] // 1 day before expiration
	public async Task If_FlagNotYetExpired_ThenPassesToNextHandler(int minutesBeforeExpiration)
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var expirationTime = evaluationTime.AddMinutes(minutesBeforeExpiration);
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			ExpirationDate = expirationTime,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain");
	}
}

public class ExpirationDateHandler_VariationHandling
{
	private readonly ExpirationDateHandler _handler;

	public ExpirationDateHandler_VariationHandling()
	{
		_handler = new ExpirationDateHandler();
	}

	[Theory]
	[InlineData("default")]
	[InlineData("control")]
	[InlineData("off")]
	[InlineData("disabled")]
	[InlineData("")]
	public async Task If_FlagExpired_ThenReturnsCorrectDefaultVariation(string defaultVariation)
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "variation-test-flag",
			ExpirationDate = evaluationTime.AddMinutes(-1),
			DefaultVariation = defaultVariation
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
		result.Reason.ShouldContain("Flag variation-test-flag expired at");
	}

	[Fact]
	public async Task If_FlagExpiredWithNullDefaultVariation_ThenReturnsNull()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "null-variation-flag",
			ExpirationDate = evaluationTime.AddMinutes(-1),
			DefaultVariation = null!
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldContain("Flag null-variation-flag expired at");
	}
}

public class ExpirationDateHandler_ReasonMessageFormatting
{
	private readonly ExpirationDateHandler _handler;

	public ExpirationDateHandler_ReasonMessageFormatting()
	{
		_handler = new ExpirationDateHandler();
	}

	[Fact]
	public async Task If_FlagExpired_ThenReasonContainsCorrectInformation()
	{
		// Arrange
		var evaluationTime = new DateTime(2023, 12, 25, 15, 30, 45, DateTimeKind.Utc);
		var expirationTime = new DateTime(2023, 12, 25, 14, 30, 45, DateTimeKind.Utc);
		var flag = new FeatureFlag
		{
			Key = "holiday-flag",
			ExpirationDate = expirationTime,
			DefaultVariation = "expired"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldContain("Flag holiday-flag expired at");
		result.Reason.ShouldContain(expirationTime.ToString());
		result.Reason.ShouldContain("current time");
		result.Reason.ShouldContain(evaluationTime.ToString());
	}

	[Theory]
	[InlineData("simple-flag")]
	[InlineData("complex_flag_name")]
	[InlineData("flag-with-dashes")]
	[InlineData("flag123")]
	[InlineData("UPPERCASE_FLAG")]
	public async Task If_DifferentFlagNames_ThenReasonContainsCorrectFlagKey(string flagKey)
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = flagKey,
			ExpirationDate = evaluationTime.AddMinutes(-1),
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldContain($"Flag {flagKey} expired at");
	}
}

public class ExpirationDateHandler_ChainOfResponsibilityIntegration
{
	private readonly ExpirationDateHandler _handler;
	private readonly Mock<IFlagEvaluationHandler> _mockNextHandler;

	public ExpirationDateHandler_ChainOfResponsibilityIntegration()
	{
		_handler = new ExpirationDateHandler();
		_mockNextHandler = new Mock<IFlagEvaluationHandler>();
	}

	[Fact]
	public async Task If_FlagNotExpired_ThenCallsNextHandler()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "not-expired-flag",
			ExpirationDate = evaluationTime.AddHours(1), // Not expired
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);
		var expectedResult = new EvaluationResult(isEnabled: true, variation: "enabled");

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
	public async Task If_FlagExpired_ThenDoesNotCallNextHandler()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "expired-flag",
			ExpirationDate = evaluationTime.AddHours(-1), // Expired
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		_mockNextHandler.Verify(x => x.Handle(It.IsAny<FeatureFlag>(), It.IsAny<EvaluationContext>()), Times.Never);
	}

	[Fact]
	public async Task If_NoNextHandler_ThenReturnsAppropriateResult()
	{
		// Arrange
		var evaluationTime = DateTime.UtcNow;
		var flag = new FeatureFlag
		{
			Key = "no-next-handler-flag",
			ExpirationDate = evaluationTime.AddHours(1), // Not expired
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: evaluationTime);

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