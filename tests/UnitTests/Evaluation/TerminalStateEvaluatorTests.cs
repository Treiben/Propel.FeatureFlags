using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Services.Evaluation;

namespace FeatureFlags.UnitTests.Evaluation;

public class TerminalStateEvaluator_CanProcess
{
	private readonly TerminalStateEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_FlagHasDisabledMode_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag();

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasEnabledMode_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag ();
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.Enabled);

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasBothDisabledAndEnabled_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag { ActiveEvaluationModes = new EvaluationModes([EvaluationMode.Enabled, EvaluationMode.Disabled]) };

		// Act & Assert
		_evaluator.CanProcess(flag, new EvaluationContext()).ShouldBeTrue();
	}

	[Theory]
	[InlineData(EvaluationMode.Scheduled)]
	[InlineData(EvaluationMode.TimeWindow)]
	[InlineData(EvaluationMode.UserTargeted)]
	[InlineData(EvaluationMode.TenantRolloutPercentage)]
	public void CanProcess_FlagHasOnlyNonTerminalModes_ReturnsFalse(EvaluationMode mode)
	{
		// Arrange
		var flag = new FeatureFlag { ActiveEvaluationModes = new EvaluationModes([mode]) };

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
			Variations = new Variations { DefaultVariation = "disabled-variation" }
		};

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
			ActiveEvaluationModes = new EvaluationModes([EvaluationMode.Enabled]),
			Variations = new Variations { DefaultVariation = "should-not-be-used" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(flag.Variations.DefaultVariation);
		result.Reason.ShouldBe("Feature flag 'test-enabled-flag' is explicitly enabled");
	}

	[Fact]
	public async Task ProcessEvaluation_FlagHasBothModes_DisabledTakesPrecedence()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "priority-test-flag",
			ActiveEvaluationModes = new EvaluationModes([EvaluationMode.Disabled, EvaluationMode.Enabled]),
			Variations = new Variations { DefaultVariation = "disabled-wins" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-wins");
		result.Reason.ShouldBe("Feature flag 'priority-test-flag' is explicitly disabled");
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
			Variations = new Variations { DefaultVariation = defaultVariation }
		};

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
			ActiveEvaluationModes = new EvaluationModes([EvaluationMode.Enabled]),
			Variations = new Variations { DefaultVariation = "should-be-ignored" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(flag.Variations.DefaultVariation); // Always "on" for enabled flags
	}
}