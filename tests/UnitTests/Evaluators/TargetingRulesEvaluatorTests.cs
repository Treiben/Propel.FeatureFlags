using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace UnitTests.Evaluators;

public class TargetingRulesEvaluatorTests
{
	[Fact]
	public async Task ProcessEvaluation_ReturnsEnabledWhenStringRuleMatches()
	{
		// Arrange
		var evaluator = new TargetingRulesEvaluator();
		var criteria = CreateCriteriaWithStringRule("userId", TargetingOperator.Equals, ["user123"], "premium");
		var context = new EvaluationContext(userId: "user123", attributes: new Dictionary<string, object> { { "userId", "user123" } });

		// Act
		var result = await evaluator.Evaluate(criteria, context);

		// Assert
		Assert.NotNull(result);
		Assert.True(result.IsEnabled);
		Assert.Equal("premium", result.Variation);
	}

	[Fact]
	public async Task ProcessEvaluation_ReturnsEnabledWhenNumericRuleMatches()
	{
		// Arrange
		var evaluator = new TargetingRulesEvaluator();
		var criteria = CreateCriteriaWithNumericRule("age", TargetingOperator.GreaterThan, [18], "adult");
		var context = new EvaluationContext(userId: "user123", attributes: new Dictionary<string, object> { { "age", "25" } });

		// Act
		var result = await evaluator.Evaluate(criteria, context);

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
		var criteria = CreateCriteriaWithStringRule("userId", TargetingOperator.Equals, ["user123"], "premium");
		var context = new EvaluationContext(attributes: new Dictionary<string, object> { { "userId", "user456" } });

		// Act
		var result = await evaluator.Evaluate(criteria, context);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.IsEnabled);
		Assert.Equal(criteria.Variations.DefaultVariation, result.Variation);
	}

	[Fact]
	public async Task ProcessEvaluation_ReturnsDisabledWhenAttributeMissing()
	{
		// Arrange
		var evaluator = new TargetingRulesEvaluator();
		var criteria = CreateCriteriaWithStringRule("userId", TargetingOperator.Equals, ["user123"], "premium");
		var context = new EvaluationContext(attributes: new Dictionary<string, object> { { "tenantId", "tenant1" } });

		// Act
		var result = await evaluator.Evaluate(criteria, context);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.IsEnabled);
	}

	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void CanProcess_ReturnsCorrectValueBasedOnTargetingRules(bool hasRules, bool expected)
	{
		// Arrange
		var evaluator = new TargetingRulesEvaluator();

		var identifier = new GlobalFlagIdentifier("test-flag");
		List<ITargetingRule> targetingRules = hasRules ? [CreateStringRule("userId", TargetingOperator.Equals, ["user123"], "on")] : [];
		var flagConfig = new EvaluationOptions(key: identifier.Key, targetingRules: targetingRules);

		var context = new EvaluationContext();

		// Act
		var result = evaluator.CanProcess(flagConfig, context);

		// Assert
		Assert.Equal(expected, result);
	}

	private static EvaluationOptions CreateCriteriaWithStringRule(string attribute, TargetingOperator op, List<string> values, string variation)
	{
		var identifier = new GlobalFlagIdentifier("test-flag");
		return new EvaluationOptions(key: identifier.Key,
			targetingRules: [CreateStringRule(attribute, op, values, variation)],
			variations: new Variations { DefaultVariation = "off" }
		);
	}

	private static EvaluationOptions CreateCriteriaWithNumericRule(string attribute, TargetingOperator op, List<double> values, string variation)
	{
		var identifier = new GlobalFlagIdentifier("test-flag");
		return new EvaluationOptions(key: identifier.Key,
		targetingRules: [CreateNumericRule(attribute, op, values, variation)],
		variations: new Variations { 
				Values = new Dictionary<string, object> { { "adult", true }, { "junior", false } },
				DefaultVariation = "junior"
			}
		);
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