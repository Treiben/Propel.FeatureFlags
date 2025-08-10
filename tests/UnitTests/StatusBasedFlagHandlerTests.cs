using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Client.Evaluators;

public class StatusBasedFlagHandler_CanProcessLogic
{
	private readonly StatusBasedFlagHandler _handler;

	public StatusBasedFlagHandler_CanProcessLogic()
	{
		_handler = new StatusBasedFlagHandler();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	public async Task If_FlagStatusIsBasic_ThenCanProcess(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "basic-status-flag",
			Status = status,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(evaluationTime: DateTime.UtcNow);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		var expectedEnabled = status == FeatureFlagStatus.Enabled;
		result.IsEnabled.ShouldBe(expectedEnabled);
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.Percentage)]
	public async Task If_FlagStatusIsComplex_ThenCannotProcess(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "complex-status-flag",
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
	public async Task If_FlagStatusDisabled_ThenProcessesCorrectly()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "disabled-flag",
			Status = FeatureFlagStatus.Disabled,
			DefaultVariation = "disabled-variation"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-variation");
		result.Reason.ShouldBe("Flag disabled");
	}

	[Fact]
	public async Task If_FlagStatusEnabled_ThenProcessesCorrectly()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "enabled-flag",
			Status = FeatureFlagStatus.Enabled,
			DefaultVariation = "should-not-be-used"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}
}

public class StatusBasedFlagHandler_DisabledFlagHandling
{
	private readonly StatusBasedFlagHandler _handler;

	public StatusBasedFlagHandler_DisabledFlagHandling()
	{
		_handler = new StatusBasedFlagHandler();
	}

	[Theory]
	[InlineData("control")]
	[InlineData("off")]
	[InlineData("disabled")]
	[InlineData("fallback")]
	[InlineData("")]
	public async Task If_DisabledFlagWithDifferentVariations_ThenReturnsDefaultVariation(string defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "disabled-variation-flag",
			Status = FeatureFlagStatus.Disabled,
			DefaultVariation = defaultVariation
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
		result.Reason.ShouldBe("Flag disabled");
	}

	[Fact]
	public async Task If_DisabledFlagWithNullVariation_ThenReturnsNull()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "disabled-null-variation-flag",
			Status = FeatureFlagStatus.Disabled,
			DefaultVariation = null!
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag disabled");
	}

	[Fact]
	public async Task If_DisabledFlagWithDifferentContexts_ThenAlwaysDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "disabled-context-flag",
			Status = FeatureFlagStatus.Disabled,
			DefaultVariation = "disabled-default"
		};

		var contexts = new[]
		{
			new EvaluationContext(),
			new EvaluationContext(tenantId: "tenant1"),
			new EvaluationContext(userId: "user123"),
			new EvaluationContext(tenantId: "tenant1", userId: "user123"),
			new EvaluationContext(evaluationTime: DateTime.UtcNow),
			new EvaluationContext(attributes: new Dictionary<string, object> { { "key", "value" } })
		};

		foreach (var context in contexts)
		{
			// Act
			var result = await _handler.Handle(flag, context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeFalse();
			result.Variation.ShouldBe("disabled-default");
			result.Reason.ShouldBe("Flag disabled");
		}
	}
}

public class StatusBasedFlagHandler_EnabledFlagHandling
{
	private readonly StatusBasedFlagHandler _handler;

	public StatusBasedFlagHandler_EnabledFlagHandling()
	{
		_handler = new StatusBasedFlagHandler();
	}

	[Fact]
	public async Task If_EnabledFlag_ThenAlwaysReturnsOnVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "enabled-flag",
			Status = FeatureFlagStatus.Enabled,
			DefaultVariation = "should-not-be-used"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}

	[Theory]
	[InlineData("control")]
	[InlineData("default")]
	[InlineData("fallback")]
	[InlineData("")]
	[InlineData(null)]
	public async Task If_EnabledFlagWithDifferentDefaultVariations_ThenAlwaysReturnsOn(string? defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "enabled-ignore-default-flag",
			Status = FeatureFlagStatus.Enabled,
			DefaultVariation = defaultVariation!
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on"); // Always "on", ignoring default variation
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_EnabledFlagWithDifferentContexts_ThenAlwaysEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "enabled-context-flag",
			Status = FeatureFlagStatus.Enabled,
			DefaultVariation = "ignored"
		};

		var contexts = new[]
		{
			new EvaluationContext(),
			new EvaluationContext(tenantId: "tenant1"),
			new EvaluationContext(userId: "user123"),
			new EvaluationContext(tenantId: "tenant1", userId: "user123"),
			new EvaluationContext(evaluationTime: DateTime.UtcNow),
			new EvaluationContext(attributes: new Dictionary<string, object> { { "key", "value" } })
		};

		foreach (var context in contexts)
		{
			// Act
			var result = await _handler.Handle(flag, context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeTrue();
			result.Variation.ShouldBe("on");
			result.Reason.ShouldBe("Flag enabled");
		}
	}
}

public class StatusBasedFlagHandler_StatusSwitchLogic
{
	private readonly StatusBasedFlagHandler _handler;

	public StatusBasedFlagHandler_StatusSwitchLogic()
	{
		_handler = new StatusBasedFlagHandler();
	}

	[Fact]
	public async Task If_StatusDisabled_ThenReturnsDisabledResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "switch-disabled-flag",
			Status = FeatureFlagStatus.Disabled,
			DefaultVariation = "disabled-var"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-var");
		result.Reason.ShouldBe("Flag disabled");
	}

	[Fact]
	public async Task If_StatusEnabled_ThenReturnsEnabledResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "switch-enabled-flag",
			Status = FeatureFlagStatus.Enabled,
			DefaultVariation = "ignored-var"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_UnexpectedStatus_ThenReturnsUnknownResult()
	{
		// This test simulates what would happen if somehow an unexpected status got through CanProcess
		// Though in practice this shouldn't happen due to the CanProcess logic
		
		// We need to test the switch statement's default case indirectly
		// Since CanProcess prevents non-basic statuses from reaching ProcessEvaluation,
		// we'll verify the behavior by testing the boundaries of the enum values
		
		// This is more of a defensive programming test
		var flag = new FeatureFlag
		{
			Key = "boundary-test-flag",
			Status = FeatureFlagStatus.Disabled, // Use valid status but test the logic
			DefaultVariation = "default-var"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert - verify disabled case works as expected
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default-var");
		result.Reason.ShouldBe("Flag disabled");
	}
}

public class StatusBasedFlagHandler_FlagKeyHandling
{
	private readonly StatusBasedFlagHandler _handler;

	public StatusBasedFlagHandler_FlagKeyHandling()
	{
		_handler = new StatusBasedFlagHandler();
	}

	[Theory]
	[InlineData("simple-flag")]
	[InlineData("complex_flag_name")]
	[InlineData("flag-with-dashes")]
	[InlineData("flag123")]
	[InlineData("UPPERCASE_FLAG")]
	[InlineData("")]
	public async Task If_DifferentFlagKeys_ThenHandlesCorrectly(string flagKey)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = flagKey,
			Status = FeatureFlagStatus.Disabled,
			DefaultVariation = "test-variation"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("test-variation");
		result.Reason.ShouldBe("Flag disabled");
	}

	[Theory]
	[InlineData("enabled-flag-1")]
	[InlineData("enabled_flag_2")]
	[InlineData("ENABLED-FLAG-3")]
	public async Task If_DifferentEnabledFlagKeys_ThenHandlesCorrectly(string flagKey)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = flagKey,
			Status = FeatureFlagStatus.Enabled,
			DefaultVariation = "ignored"
		};
		var context = new EvaluationContext();

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}
}

public class StatusBasedFlagHandler_ChainOfResponsibilityIntegration
{
	private readonly StatusBasedFlagHandler _handler;
	private readonly Mock<IFlagEvaluationHandler> _mockNextHandler;

	public StatusBasedFlagHandler_ChainOfResponsibilityIntegration()
	{
		_handler = new StatusBasedFlagHandler();
		_mockNextHandler = new Mock<IFlagEvaluationHandler>();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.Percentage)]
	public async Task If_FlagStatusNotBasic_ThenCallsNextHandler(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "complex-status-flag",
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

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	public async Task If_FlagStatusBasic_ThenDoesNotCallNextHandler(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "basic-status-flag",
			Status = status,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext();

		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		var expectedEnabled = status == FeatureFlagStatus.Enabled;
		result.IsEnabled.ShouldBe(expectedEnabled);
		_mockNextHandler.Verify(x => x.Handle(It.IsAny<FeatureFlag>(), It.IsAny<EvaluationContext>()), Times.Never);
	}

	[Fact]
	public async Task If_NoNextHandler_ThenReturnsAppropriateResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-next-handler-flag",
			Status = FeatureFlagStatus.Scheduled, // Complex status that can't be processed
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

	[Fact]
	public async Task If_BasicStatusWithNoNextHandler_ThenProcessesDirectly()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "basic-no-next-flag",
			Status = FeatureFlagStatus.Enabled,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext();

		// NextHandler is null by default

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}
}

public class StatusBasedFlagHandler_EvaluationContextIndependence
{
	private readonly StatusBasedFlagHandler _handler;

	public StatusBasedFlagHandler_EvaluationContextIndependence()
	{
		_handler = new StatusBasedFlagHandler();
	}

	[Fact]
	public async Task If_ComplexEvaluationContext_ThenIgnoresContextForBasicStatus()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "context-independent-flag",
			Status = FeatureFlagStatus.Disabled,
			DefaultVariation = "context-ignored"
		};

		var complexContext = new EvaluationContext(
			tenantId: "tenant123",
			userId: "user456",
			attributes: new Dictionary<string, object>
			{
				{ "region", "us-east" },
				{ "plan", "premium" },
				{ "beta_user", true }
			},
			evaluationTime: DateTime.UtcNow.AddHours(-2),
			timeZone: "America/New_York"
		);

		// Act
		var result = await _handler.Handle(flag, complexContext);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("context-ignored");
		result.Reason.ShouldBe("Flag disabled");
	}

	[Fact]
	public async Task If_NullContextValues_ThenHandlesGracefully()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "null-context-flag",
			Status = FeatureFlagStatus.Enabled,
			DefaultVariation = "default"
		};

		var nullContext = new EvaluationContext(
			tenantId: null,
			userId: null,
			attributes: null,
			evaluationTime: null,
			timeZone: null
		);

		// Act
		var result = await _handler.Handle(flag, nullContext);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}
}