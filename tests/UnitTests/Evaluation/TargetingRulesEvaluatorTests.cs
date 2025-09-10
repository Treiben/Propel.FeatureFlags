using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace Propel.FeatureFlags.Tests.Evaluation.Handlers;

public class TargetingRulesEvaluatorTests
{
	[Fact]
	public async Task ProcessEvaluation_ReturnsEnabledWhenStringRuleMatches()
	{
		// Arrange
		var evaluator = new TargetingRulesEvaluator();
		var flag = CreateFeatureFlagWithStringRule("userId", TargetingOperator.Equals, ["user123"], "premium");
		var context = new EvaluationContext(userId: "user123", attributes: new Dictionary<string, object> { { "userId", "user123" } });

		// Act
		var result = await evaluator.ProcessEvaluation(flag, context);

		// Assert
		Assert.NotNull(result);
		Assert.True(result.IsEnabled);
		Assert.Equal("premium", result.Variation);
		Assert.Contains("Targeting rule matched", result.Reason);
	}

	[Fact]
	public async Task ProcessEvaluation_ReturnsEnabledWhenNumericRuleMatches()
	{
		// Arrange
		var evaluator = new TargetingRulesEvaluator();
		var flag = CreateFeatureFlagWithNumericRule("age", TargetingOperator.GreaterThan, [18], "adult");
		var context = new EvaluationContext(attributes: new Dictionary<string, object> { { "age", "25" } });

		// Act
		var result = await evaluator.ProcessEvaluation(flag, context);

		// Assert
		Assert.NotNull(result);
		Assert.True(result.IsEnabled);
		Assert.Equal("adult", result.Variation);
	}

	[Fact]
	public async Task ProcessEvaluation_ReturnsDisabledWhenNoRulesMatch()
	{
		// Arrange
		var evaluator = new TargetingRulesEvaluator();
		var flag = CreateFeatureFlagWithStringRule("userId", TargetingOperator.Equals, ["user123"], "premium");
		var context = new EvaluationContext(attributes: new Dictionary<string, object> { { "userId", "user456" } });

		// Act
		var result = await evaluator.ProcessEvaluation(flag, context);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.IsEnabled);
		Assert.Equal(flag.Variations.DefaultVariation, result.Variation);
		Assert.Equal("No targeting rules matched", result.Reason);
	}

	[Fact]
	public async Task ProcessEvaluation_ReturnsDisabledWhenAttributeMissing()
	{
		// Arrange
		var evaluator = new TargetingRulesEvaluator();
		var flag = CreateFeatureFlagWithStringRule("userId", TargetingOperator.Equals, ["user123"], "premium");
		var context = new EvaluationContext(attributes: new Dictionary<string, object> { { "tenantId", "tenant1" } });

		// Act
		var result = await evaluator.ProcessEvaluation(flag, context);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.IsEnabled);
		Assert.Equal("No targeting rules matched", result.Reason);
	}

	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void CanProcess_ReturnsCorrectValueBasedOnTargetingRules(bool hasRules, bool expected)
	{
		// Arrange
		var evaluator = new TargetingRulesEvaluator();
		var flag = new FeatureFlag
		{
			TargetingRules = hasRules ? [CreateStringRule("userId", TargetingOperator.Equals, ["user123"], "on")] : []
		};
		var context = new EvaluationContext();

		// Act
		var result = evaluator.CanProcess(flag, context);

		// Assert
		Assert.Equal(expected, result);
	}

	private static FeatureFlag CreateFeatureFlagWithStringRule(string attribute, TargetingOperator op, List<string> values, string variation)
	{
		return new FeatureFlag
		{
			TargetingRules = [CreateStringRule(attribute, op, values, variation)],
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
	}

	private static FeatureFlag CreateFeatureFlagWithNumericRule(string attribute, TargetingOperator op, List<double> values, string variation)
	{
		return new FeatureFlag
		{
			TargetingRules = [CreateNumericRule(attribute, op, values, variation)],
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
	}

	private static StringTargetingRule CreateStringRule(string attribute, TargetingOperator op, List<string> values, string variation)
	{
		return new StringTargetingRule
		{
			Attribute = attribute,
			Operator = op,
			Values = values,
			Variation = variation
		};
	}

	private static NumericTargetingRule CreateNumericRule(string attribute, TargetingOperator op, List<double> values, string variation)
	{
		return new NumericTargetingRule
		{
			Attribute = attribute,
			Operator = op,
			Values = values,
			Variation = variation
		};
	}
}