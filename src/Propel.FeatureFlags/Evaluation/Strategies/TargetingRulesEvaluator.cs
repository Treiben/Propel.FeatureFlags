using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Strategies;

public sealed class TargetingRulesEvaluator : IOrderedEvaluator
{
	public EvaluationOrder EvaluationOrder => EvaluationOrder.CustomTargeting;

	public bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		//return flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.UserTargeted, FlagEvaluationMode.TenantTargeted]);
		return flag.TargetingRules != null && flag.TargetingRules.Count > 0;
	}

	public async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		// Evaluate targeting rules
		foreach (var rule in flag.TargetingRules)
		{
			if (EvaluateTargetingRule(rule, context.Attributes))
			{
				return new EvaluationResult(isEnabled: true, variation: rule.Variation ?? "on",
					reason: $"Targeting rule matched: {rule}");
			}
		}

		return new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, reason: "No targeting rules matched");
	}

	private bool EvaluateTargetingRule(ITargetingRule rule, Dictionary<string, object> attributes)
	{
		if (!attributes.TryGetValue(rule.Attribute, out var attributeValue))
		{
			return false;
		}

		var stringValue = attributeValue?.ToString() ?? string.Empty;

		if (rule is NumericTargetingRule && double.TryParse(stringValue, out var numValue))
			return ((NumericTargetingRule)rule).EvaluateFor(numValue);

		return ((StringTargetingRule)rule).EvaluateFor(stringValue);

	}
}
