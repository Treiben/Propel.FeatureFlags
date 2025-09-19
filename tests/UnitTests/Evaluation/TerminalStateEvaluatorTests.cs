using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Evaluation;

namespace FeatureFlags.UnitTests.Evaluation;

public class TerminalStateEvaluator_CanProcess
{
	private readonly TerminalStateEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_FlagHasDisabledMode_ReturnsTrue()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new FlagEvaluationConfiguration(identifier);

		// Act & Assert
		_evaluator.CanProcess(flagConfig, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasEnabledMode_ReturnsTrue()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new FlagEvaluationConfiguration(identifier);
		flagConfig.ActiveEvaluationModes.AddMode(EvaluationMode.On);

		// Act & Assert
		_evaluator.CanProcess(flagConfig, new EvaluationContext()).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_FlagHasBothDisabledAndEnabled_ReturnsTrue()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var modes = new EvaluationModes([EvaluationMode.On, EvaluationMode.Off]);
		var flagConfig = new FlagEvaluationConfiguration(identifier: identifier, activeEvaluationModes: modes);

		// Act & Assert
		_evaluator.CanProcess(flagConfig, new EvaluationContext()).ShouldBeTrue();
	}

	[Theory]
	[InlineData(EvaluationMode.Scheduled)]
	[InlineData(EvaluationMode.TimeWindow)]
	[InlineData(EvaluationMode.UserTargeted)]
	[InlineData(EvaluationMode.TenantRolloutPercentage)]
	public void CanProcess_FlagHasOnlyNonTerminalModes_ReturnsFalse(EvaluationMode mode)
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var modes = new EvaluationModes([mode]);
		var flagConfig = new FlagEvaluationConfiguration(identifier: identifier, activeEvaluationModes: modes);

		// Act & Assert
		_evaluator.CanProcess(flagConfig, new EvaluationContext()).ShouldBeFalse();
	}

	[Fact]
	public void CanProcess_FlagHasDefaultDisabledMode_ReturnsTrue()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new FlagEvaluationConfiguration(identifier); // Default constructor creates Disabled mode

		// Act & Assert
		_evaluator.CanProcess(flagConfig, new EvaluationContext()).ShouldBeTrue();
	}
}

public class TerminalStateEvaluator_ProcessEvaluation
{
	private readonly TerminalStateEvaluator _evaluator = new();

	[Fact]
	public async Task ProcessEvaluation_FlagIsDisabled_ReturnsDisabledResult()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-disabled-flag", Scope.Global);
		var variations = new Variations { DefaultVariation = "disabled-variation" };
		var flagConfig = new FlagEvaluationConfiguration(identifier: identifier, variations: variations);

		// Act
		var result = await _evaluator.ProcessEvaluation(flagConfig, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-variation");
		result.Reason.ShouldBe("Feature flag 'test-disabled-flag' is explicitly disabled");
	}

	[Fact]
	public async Task ProcessEvaluation_FlagIsEnabled_ReturnsEnabledResult()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-enabled-flag", Scope.Global);
		var activeEvaluationModes = new EvaluationModes([EvaluationMode.On]);
		var variations = new Variations { DefaultVariation = "should-not-be-used" };
		var flagConfig = new FlagEvaluationConfiguration(identifier: identifier, activeEvaluationModes: activeEvaluationModes, variations: variations);

		// Act
		var result = await _evaluator.ProcessEvaluation(flagConfig, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(flagConfig.Variations.DefaultVariation);
		result.Reason.ShouldBe("Feature flag 'test-enabled-flag' is explicitly enabled");
	}

	[Fact]
	public async Task ProcessEvaluation_FlagHasBothModes_DisabledTakesPrecedence()
	{
		// Arrange
		var identifier = new FlagIdentifier("priority-test-flag", Scope.Global);
		var activeEvaluationModes = new EvaluationModes([EvaluationMode.Off, EvaluationMode.On]);
		var variations = new Variations { DefaultVariation = "disabled-wins" };
		var flagConfig = new FlagEvaluationConfiguration(identifier: identifier, activeEvaluationModes: activeEvaluationModes, variations: variations);

		// Act
		var result = await _evaluator.ProcessEvaluation(flagConfig, new EvaluationContext());

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
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var variations = new Variations { DefaultVariation = defaultVariation };
		var flagConfig = new FlagEvaluationConfiguration(identifier: identifier, variations: variations);

		// Act
		var result = await _evaluator.ProcessEvaluation(flagConfig, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
	}

	[Fact]
	public async Task ProcessEvaluation_EnabledFlag_AlwaysReturnsOn()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var activeEvaluationModes = new EvaluationModes([EvaluationMode.On]);
		var variations = new Variations { DefaultVariation = "should-be-ignored" };
		var flagConfig = new FlagEvaluationConfiguration(identifier: identifier, activeEvaluationModes: activeEvaluationModes, variations: variations);

		// Act
		var result = await _evaluator.ProcessEvaluation(flagConfig, new EvaluationContext());

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(flagConfig.Variations.DefaultVariation); // Always "on" for enabled flags
	}
}