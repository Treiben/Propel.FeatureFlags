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
		var criteria = new EvaluationCriteria();

		// Act & Assert
		_evaluator.CanProcess(criteria, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasEnabledMode_ReturnsTrue()
	{
		// Arrange
		var criteria = new EvaluationCriteria();
		criteria.ActiveEvaluationModes.AddMode(EvaluationMode.Enabled);

		// Act & Assert
		_evaluator.CanProcess(criteria, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasBothDisabledAndEnabled_ReturnsTrue()
	{
		// Arrange
		var criteria = new EvaluationCriteria { ActiveEvaluationModes = new EvaluationModes([EvaluationMode.Enabled, EvaluationMode.Disabled]) };

		// Act & Assert
		_evaluator.CanProcess(criteria, new EvaluationContext()).ShouldBeTrue();
	}

	[Theory]
	[InlineData(EvaluationMode.Scheduled)]
	[InlineData(EvaluationMode.TimeWindow)]
	[InlineData(EvaluationMode.UserTargeted)]
	[InlineData(EvaluationMode.TenantRolloutPercentage)]
	public void CanProcess_FlagHasOnlyNonTerminalModes_ReturnsFalse(EvaluationMode mode)
	{
		// Arrange
		var criteria = new EvaluationCriteria { ActiveEvaluationModes = new EvaluationModes([mode]) };

		// Act & Assert
		_evaluator.CanProcess(criteria, new EvaluationContext()).ShouldBeFalse();
	}

	[Fact]
	public void CanProcess_FlagHasDefaultDisabledMode_ReturnsTrue()
	{
		// Arrange
		var criteria = new EvaluationCriteria(); // Default constructor creates Disabled mode

		// Act & Assert
		_evaluator.CanProcess(criteria, new EvaluationContext()).ShouldBeTrue();
	}
}

public class TerminalStateEvaluator_ProcessEvaluation
{
	private readonly TerminalStateEvaluator _evaluator = new();

	[Fact]
	public async Task ProcessEvaluation_FlagIsDisabled_ReturnsDisabledResult()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "test-disabled-flag",
			Variations = new Variations { DefaultVariation = "disabled-variation" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(criteria, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-variation");
		result.Reason.ShouldBe("Feature flag 'test-disabled-flag' is explicitly disabled");
	}

	[Fact]
	public async Task ProcessEvaluation_FlagIsEnabled_ReturnsEnabledResult()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "test-enabled-flag",
			ActiveEvaluationModes = new EvaluationModes([EvaluationMode.Enabled]),
			Variations = new Variations { DefaultVariation = "should-not-be-used" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(criteria, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(criteria.Variations.DefaultVariation);
		result.Reason.ShouldBe("Feature flag 'test-enabled-flag' is explicitly enabled");
	}

	[Fact]
	public async Task ProcessEvaluation_FlagHasBothModes_DisabledTakesPrecedence()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "priority-test-flag",
			ActiveEvaluationModes = new EvaluationModes([EvaluationMode.Disabled, EvaluationMode.Enabled]),
			Variations = new Variations { DefaultVariation = "disabled-wins" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(criteria, new EvaluationContext());

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
		var criteria = new EvaluationCriteria
		{
			FlagKey = "variation-test",
			Variations = new Variations { DefaultVariation = defaultVariation }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(criteria, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
	}

	[Fact]
	public async Task ProcessEvaluation_EnabledFlag_AlwaysReturnsOn()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "always-on-test",
			ActiveEvaluationModes = new EvaluationModes([EvaluationMode.Enabled]),
			Variations = new Variations { DefaultVariation = "should-be-ignored" }
		};

		// Act
		var result = await _evaluator.ProcessEvaluation(criteria, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(criteria.Variations.DefaultVariation); // Always "on" for enabled flags
	}
}