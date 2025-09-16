using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Services.Evaluation;

public sealed class TargetingRulesEvaluator : OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.CustomTargeting;

	public override bool CanProcess(EvaluationCriteria flag, EvaluationContext context)
	{
		return flag.TargetingRules != null && flag.TargetingRules.Count > 0;
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(EvaluationCriteria flag, EvaluationContext context)
	{
		var id = context.TenantId ?? context.UserId;
		if (string.IsNullOrWhiteSpace(id))
		{
			return new EvaluationResult(isEnabled: false,
				variation: flag.Variations.DefaultVariation,
				reason: "Targeting rule matched but no tenant or user ID provided for variation selection");
		}

		// Evaluate targeting rules
		foreach (var rule in flag.TargetingRules)
		{
			if (EvaluateTargetingRule(rule, context.Attributes))
			{
				return CreateEvaluationResult(flag, context, isActive: true, rule.Variation,
					because: $"Targeting rule matched: {rule}");
			}
		}

		return CreateEvaluationResult(flag, context, isActive: false,
			because: "No targeting rules matched");
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
