using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public sealed class TargetingRulesEvaluator : EvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.CustomTargeting;

	public override bool CanProcess(EvaluationOptions options, EvaluationContext context)
	{
		return options.TargetingRules != null && options.TargetingRules.Count > 0;
	}

	public override ValueTask<EvaluationResult?> Evaluate(EvaluationOptions options, EvaluationContext context)
	{
		var id = context.TenantId ?? context.UserId;

		if (string.IsNullOrWhiteSpace(id))
		{
			var result = new EvaluationResult(isEnabled: false,
				variation: options.Variations.DefaultVariation,
				reason: "Targeting rule matched but no tenant or user ID provided for variation selection");
			return new ValueTask<EvaluationResult?>(result);
		}

		// Evaluate targeting rules
		foreach (var rule in options.TargetingRules)
		{
			if (EvaluateTargetingRule(rule, context.Attributes))
			{
				var result = CreateEvaluationResult(options: options, isActive: true, variation: rule.Variation,
					because: $"Targeting rule matched: {rule}");
				return new ValueTask<EvaluationResult?>(result);
			}
		}

		return new ValueTask<EvaluationResult?>(CreateEvaluationResult(options, context, isActive: false,
			because: "No targeting rules matched"));
	}

	private bool EvaluateTargetingRule(ITargetingRule targetingRule, Dictionary<string, object> attributes)
	{
		if (!attributes.TryGetValue(targetingRule.Attribute, out var attributeValue))
		{
			return false;
		}

		var stringValue = attributeValue?.ToString() ?? string.Empty;

		switch (targetingRule)
		{
			case NumericTargetingRule numericRule when double.TryParse(stringValue, out var numValue):
				return numericRule.EvaluateFor(numValue);
			case StringTargetingRule stringRule:
				return stringRule.EvaluateFor(stringValue);
			default:
				return false; // Unknown rule type
		}
	}
}
