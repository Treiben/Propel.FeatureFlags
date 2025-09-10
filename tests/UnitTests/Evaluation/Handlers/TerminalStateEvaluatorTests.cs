using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace FeatureFlags.UnitTests.Evaluation.Handlers;

public class TerminalStateEvaluator_CanProcess
{
	private readonly TerminalStateEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_FlagHasDisabledMode_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag { EvaluationModeSet = new FlagEvaluationModeSet() };
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasEnabledMode_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag { EvaluationModeSet = new FlagEvaluationModeSet() };
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasBothDisabledAndEnabled_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag { EvaluationModeSet = new FlagEvaluationModeSet() };
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Theory]
	[InlineData(FlagEvaluationMode.Scheduled)]
	[InlineData(FlagEvaluationMode.TimeWindow)]
	[InlineData(FlagEvaluationMode.UserTargeted)]
	[InlineData(FlagEvaluationMode.TenantRolloutPercentage)]
	public void CanProcess_FlagHasOnlyNonTerminalModes_ReturnsFalse(FlagEvaluationMode mode)
	{
		// Arrange
		var flag = new FeatureFlag { EvaluationModeSet = new FlagEvaluationModeSet() };
		flag.EvaluationModeSet.AddMode(mode);

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeFalse();
	}

	[Fact]
	public void CanProcess_FlagHasDefaultDisabledMode_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag(); // Default constructor creates Disabled mode

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}
}

public class TerminalStateEvaluator_ProcessEvaluation
{
	private readonly TerminalStateEvaluator _evaluator = new();

	[Fact]
	public async Task ProcessEvaluation_FlagIsDisabled_ReturnsDisabledResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-disabled-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "disabled-variation" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-variation");
		result.Reason.ShouldBe("Feature flag 'test-disabled-flag' is explicitly disabled");
	}

	[Fact]
	public async Task ProcessEvaluation_FlagIsEnabled_ReturnsEnabledResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-enabled-flag",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "should-not-be-used" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Feature flag 'test-enabled-flag' is explicitly enabled");
	}

	[Fact]
	public async Task ProcessEvaluation_FlagHasBothModes_DisabledTakesPrecedence()
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

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-wins");
		result.Reason.ShouldBe("Feature flag 'priority-test-flag' is explicitly disabled");
	}

	[Fact]
	public async Task ProcessEvaluation_NullVariations_HandlesGracefully()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "null-variations-flag",
			Variations = null
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off"); // Default fallback
		result.Reason.ShouldBe("Feature flag 'null-variations-flag' is explicitly disabled");
	}

	[Theory]
	[InlineData("maintenance-mode")]
	[InlineData("off")]
	[InlineData("")]
	[InlineData("custom-fallback")]
	public async Task ProcessEvaluation_DisabledFlag_ReturnsCorrectVariation(string defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "variation-test",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = defaultVariation }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
	}

	[Fact]
	public async Task ProcessEvaluation_EnabledFlag_AlwaysReturnsOn()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "always-on-test",
			EvaluationModeSet = new FlagEvaluationModeSet(),
			Variations = new FlagVariations { DefaultVariation = "should-be-ignored" }
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on"); // Always "on" for enabled flags
	}
}