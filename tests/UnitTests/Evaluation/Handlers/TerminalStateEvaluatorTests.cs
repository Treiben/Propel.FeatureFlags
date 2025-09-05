using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace FeatureFlags.UnitTests.Evaluation.Handlers;

public class TerminalStateEvaluator_EvaluationOrder
{
	[Fact]
	public void EvaluationOrder_ShouldReturnTerminal()
	{
		// Arrange
		var evaluator = new TerminalStateEvaluator();

		// Act
		var order = evaluator.EvaluationOrder;

		// Assert
		order.ShouldBe(EvaluationOrder.Terminal);
	}
}
public class TerminalStateEvaluator_CanProcess
{
	private readonly TerminalStateEvaluator _evaluator;

	public TerminalStateEvaluator_CanProcess()
	{
		_evaluator = new TerminalStateEvaluator();
	}

	[Fact]
	public void If_FlagContainsDisabledMode_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_FlagContainsEnabledMode_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_FlagContainsBothDisabledAndEnabledModes_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_FlagContainsDisableAddedAfterOtherModes_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);
		
		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_FlagContainsEnabledWithOtherModes_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserRolloutPercentage);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData(FlagEvaluationMode.Scheduled)]
	[InlineData(FlagEvaluationMode.TimeWindow)]
	[InlineData(FlagEvaluationMode.UserTargeted)]
	[InlineData(FlagEvaluationMode.UserRolloutPercentage)]
	[InlineData(FlagEvaluationMode.TenantRolloutPercentage)]
	public void If_FlagDoesNotContainDisabledOrEnabledModes_ThenCannotProcess(FlagEvaluationMode mode)
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
	public void If_FlagContainsOnlyNonTerminalModes_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_FlagHasNoModes_ThenDefaultModeIsDisabled_AndCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag();
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}
}

public class TerminalStateEvaluator_ProcessEvaluation_DisabledFlag
{
	private readonly TerminalStateEvaluator _evaluator;

	public TerminalStateEvaluator_ProcessEvaluation_DisabledFlag()
	{
		_evaluator = new TerminalStateEvaluator();
	}

	[Fact]
	public async Task If_FlagIsDisabled_ThenReturnsDisabledResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-disabled-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "disabled-variation" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-variation");
		result.Reason.ShouldBe("Feature flag 'test-disabled-flag' is explicitly disabled");
	}

	[Fact]
	public async Task If_FlagIsDisabledWithDefaultVariationEmpty_ThenReturnsEmptyVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "empty-variation-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("");
		result.Reason.ShouldBe("Feature flag 'empty-variation-flag' is explicitly disabled");
	}

	[Fact]
	public async Task If_FlagIsDisabledWithCustomDefaultVariation_ThenReturnsCustomVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "custom-disabled-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "custom-disabled-state" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("custom-disabled-state");
		result.Reason.ShouldBe("Feature flag 'custom-disabled-flag' is explicitly disabled");
	}

	[Fact]
	public async Task If_FlagHasDisabledModeAddedAfterOtherModes_ThenReturnsDisabledResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "mixed-modes-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "disabled-priority" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-priority");
		result.Reason.ShouldBe("Feature flag 'mixed-modes-flag' is explicitly disabled");
	}
}

public class TerminalStateEvaluator_ProcessEvaluation_EnabledFlag
{
	private readonly TerminalStateEvaluator _evaluator;

	public TerminalStateEvaluator_ProcessEvaluation_EnabledFlag()
	{
		_evaluator = new TerminalStateEvaluator();
	}

	[Fact]
	public async Task If_FlagIsEnabled_ThenReturnsEnabledResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-enabled-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "should-not-be-used" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Feature flag 'test-enabled-flag' is explicitly enabled");
	}

	[Fact]
	public async Task If_FlagIsEnabledAlwaysReturnsOnVariation_RegardlessOfDefaultVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "enabled-with-custom-default",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "custom-default" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on"); // Always "on" for enabled flags
		result.Reason.ShouldBe("Feature flag 'enabled-with-custom-default' is explicitly enabled");
	}

	[Fact]
	public async Task If_FlagHasEnabledModeWithOtherModesButNoDisabled_ThenReturnsEnabledResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "enabled-mixed-modes-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "fallback" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Feature flag 'enabled-mixed-modes-flag' is explicitly enabled");
	}
}

public class TerminalStateEvaluator_ProcessEvaluation_FlagKeyInReason
{
	private readonly TerminalStateEvaluator _evaluator;

	public TerminalStateEvaluator_ProcessEvaluation_FlagKeyInReason()
	{
		_evaluator = new TerminalStateEvaluator();
	}

	[Theory]
	[InlineData("feature-toggle")]
	[InlineData("new-ui-enabled")]
	[InlineData("payment-gateway-v2")]
	[InlineData("")]
	[InlineData("flag-with-special-chars!@#$%")]
	public async Task If_FlagKeyProvided_ThenIncludesKeyInReason(string flagKey)
	{
		// Arrange
		var disabledFlag = new FeatureFlag
		{
			Key = flagKey,
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		disabledFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);

		var enabledFlag = new FeatureFlag
		{
			Key = flagKey,
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		enabledFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);

		var context = new EvaluationContext();

		// Act
		var disabledResult = await _evaluator.ProcessEvaluation(disabledFlag, context);
		var enabledResult = await _evaluator.ProcessEvaluation(enabledFlag, context);

		// Assert
		disabledResult.ShouldNotBeNull();
		disabledResult.Reason.ShouldBe($"Feature flag '{flagKey}' is explicitly disabled");

		enabledResult.ShouldNotBeNull();
		enabledResult.Reason.ShouldBe($"Feature flag '{flagKey}' is explicitly enabled");
	}

	[Fact]
	public async Task If_FlagKeyIsNull_ThenHandlesGracefully()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = null!,
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldBe("Feature flag '' is explicitly disabled");
	}
}

public class TerminalStateEvaluator_ProcessEvaluation_PriorityLogic
{
	private readonly TerminalStateEvaluator _evaluator;

	public TerminalStateEvaluator_ProcessEvaluation_PriorityLogic()
	{
		_evaluator = new TerminalStateEvaluator();
	}

	[Fact]
	public async Task If_FlagHasBothDisabledAndEnabled_ThenDisabledTakesPrecedence()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "priority-test-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "disabled-wins" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-wins");
		result.Reason.ShouldBe("Feature flag 'priority-test-flag' is explicitly disabled");
	}

	[Fact]
	public async Task If_OnlyEnabledModePresent_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "only-enabled-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "unused" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Feature flag 'only-enabled-flag' is explicitly enabled");
	}

	[Fact]
	public async Task If_EnabledModeAddedFirst_ThenDisabledStillTakesPrecedence()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "precedence-test-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "disabled-precedence" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-precedence");
		result.Reason.ShouldBe("Feature flag 'precedence-test-flag' is explicitly disabled");
	}
}

public class TerminalStateEvaluator_ProcessEvaluation_EdgeCases
{
	private readonly TerminalStateEvaluator _evaluator;

	public TerminalStateEvaluator_ProcessEvaluation_EdgeCases()
	{
		_evaluator = new TerminalStateEvaluator();
	}

	[Fact]
	public async Task If_VariationsIsNull_ThenHandlesGracefully()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "null-variations-flag",
			Variations = null!
		};

		var context = new EvaluationContext();

		// Act & Assert - Should handle gracefully without throwing
		var result = await _evaluator.ProcessEvaluation(flag, context);
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Feature flag 'null-variations-flag' is explicitly disabled");
	}

	[Fact]
	public async Task If_ContextIsNull_ThenStillProcesses()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "null-context-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "context-null" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, null!);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Feature flag 'null-context-flag' is explicitly enabled");
	}

	[Fact]
	public async Task If_FlagHasComplexMixOfModesAddedAfterDisabled_ThenDisabledModeIsRemoved()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "complex-modes-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "complex-disabled" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserRolloutPercentage);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TenantRolloutPercentage);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}
}

public class TerminalStateEvaluator_ProcessEvaluation_RealWorldScenarios
{
	private readonly TerminalStateEvaluator _evaluator;

	public TerminalStateEvaluator_ProcessEvaluation_RealWorldScenarios()
	{
		_evaluator = new TerminalStateEvaluator();
	}

	[Fact]
	public async Task MaintenanceMode_DisabledFlag_ShouldWorkCorrectly()
	{
		// Arrange - Feature disabled during maintenance
		var flag = new FeatureFlag
		{
			Key = "payment-processing",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "maintenance-mode" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext(userId: "user123", tenantId: "tenant456");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("maintenance-mode");
		result.Reason.ShouldBe("Feature flag 'payment-processing' is explicitly disabled");
	}

	[Fact]
	public async Task GlobalFeature_AlwaysEnabled_ShouldWorkCorrectly()
	{
		// Arrange - Feature that should always be available
		var flag = new FeatureFlag
		{
			Key = "user-authentication",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "auth-disabled" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Feature flag 'user-authentication' is explicitly enabled");
	}

	[Fact]
	public async Task DeprecatedFeature_PermanentlyDisabled_ShouldWorkCorrectly()
	{
		// Arrange - Old feature that's been permanently disabled
		var flag = new FeatureFlag
		{
			Key = "legacy-dashboard-v1",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "show-upgrade-message" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext(
			userId: "power-user", 
			attributes: new Dictionary<string, object> { { "beta-access", true } });

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("show-upgrade-message");
		result.Reason.ShouldBe("Feature flag 'legacy-dashboard-v1' is explicitly disabled");
	}

	[Fact]
	public async Task EmergencyOverride_DisabledFeature_ShouldWorkCorrectly()
	{
		// Arrange - Feature disabled due to emergency override
		var flag = new FeatureFlag
		{
			Key = "third-party-integration",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "emergency-fallback" }
		};
		// Initially had other evaluation modes, but emergency disabled
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled); // Emergency override
		var context = new EvaluationContext(tenantId: "premium-tenant");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("emergency-fallback");
		result.Reason.ShouldBe("Feature flag 'third-party-integration' is explicitly disabled");
	}

	[Fact]
	public async Task CoreFunctionality_AlwaysEnabled_ShouldWorkCorrectly()
	{
		// Arrange - Core functionality that must always be available
		var flag = new FeatureFlag
		{
			Key = "data-persistence",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "read-only-mode" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		var context = new EvaluationContext(
			tenantId: "critical-tenant",
			userId: "system-admin",
			attributes: new Dictionary<string, object> 
			{ 
				{ "priority", "high" },
				{ "environment", "production" }
			});

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Feature flag 'data-persistence' is explicitly enabled");
	}

	[Fact]
	public async Task MultipleFeatures_DifferentStates_ShouldProcessIndependently()
	{
		// Arrange - Test multiple flags with different terminal states
		var disabledFlag = new FeatureFlag
		{
			Key = "experimental-ui",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "classic-ui" }
		};
		disabledFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);

		var enabledFlag = new FeatureFlag
		{
			Key = "security-logging",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "no-logging" }
		};
		enabledFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);

		var context = new EvaluationContext(userId: "test-user");

		// Act
		var disabledResult = await _evaluator.ProcessEvaluation(disabledFlag, context);
		var enabledResult = await _evaluator.ProcessEvaluation(enabledFlag, context);

		// Assert
		disabledResult.IsEnabled.ShouldBeFalse();
		disabledResult.Variation.ShouldBe("classic-ui");
		disabledResult.Reason.ShouldBe("Feature flag 'experimental-ui' is explicitly disabled");

		enabledResult.IsEnabled.ShouldBeTrue();
		enabledResult.Variation.ShouldBe("on");
		enabledResult.Reason.ShouldBe("Feature flag 'security-logging' is explicitly enabled");
	}
}

public class TerminalStateEvaluator_ProcessEvaluation_VariationScenarios
{
	private readonly TerminalStateEvaluator _evaluator;

	public TerminalStateEvaluator_ProcessEvaluation_VariationScenarios()
	{
		_evaluator = new TerminalStateEvaluator();
	}

	[Theory]
	[InlineData("off")]
	[InlineData("disabled")]
	[InlineData("maintenance")]
	[InlineData("fallback")]
	[InlineData("v1")]
	[InlineData("")]
	public async Task If_DisabledFlagWithVariousDefaultVariations_ThenReturnsCorrectVariation(string defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "variation-test-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = defaultVariation }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
	}

	[Theory]
	[InlineData("off")]
	[InlineData("disabled")]
	[InlineData("custom")]
	[InlineData("anything")]
	[InlineData("")]
	public async Task If_EnabledFlagAlwaysReturnsOn_RegardlessOfDefaultVariation(string defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "always-on-test",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = defaultVariation }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on"); // Always "on" regardless of defaultVariation
	}

	[Fact]
	public async Task If_ComplexVariationNames_ThenHandledCorrectly()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "complex-variation-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "feature-disabled-use-legacy-implementation-v2.1" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("feature-disabled-use-legacy-implementation-v2.1");
	}
}

public class TerminalStateEvaluator_ProcessEvaluation_AsyncBehavior
{
	private readonly TerminalStateEvaluator _evaluator;

	public TerminalStateEvaluator_ProcessEvaluation_AsyncBehavior()
	{
		_evaluator = new TerminalStateEvaluator();
	}

	[Fact]
	public async Task ProcessEvaluation_CompletesImmediately()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "async-test-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		var context = new EvaluationContext();

		// Act
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var result = await _evaluator.ProcessEvaluation(flag, context);
		stopwatch.Stop();

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		// Should complete very quickly since no actual async work is done
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10);
	}

	[Fact]
	public async Task ProcessEvaluation_CanBeCalledConcurrently()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "concurrent-test-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "concurrent-off" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);
		var context = new EvaluationContext();

		// Act - Run multiple concurrent evaluations
		var tasks = Enumerable.Range(0, 100)
			.Select(_ => _evaluator.ProcessEvaluation(flag, context))
			.ToArray();

		var results = await Task.WhenAll(tasks);

		// Assert - All results should be identical
		foreach (var result in results)
		{
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeFalse();
			result.Variation.ShouldBe("concurrent-off");
			result.Reason.ShouldBe("Feature flag 'concurrent-test-flag' is explicitly disabled");
		}
	}
}