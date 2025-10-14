using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

/// <summary>
/// Evaluates targeting rules to determine whether a specific variation should be applied based on the provided
/// evaluation context and options.
/// </summary>
/// <remarks>This evaluator processes targeting rules defined in the <see
/// cref="EvaluationOptions.TargetingRules"/>  collection. Each rule is evaluated against the attributes in the <see
/// cref="EvaluationContext"/> to  determine if a match occurs. If a rule matches, the corresponding variation is
/// selected. If no rules  match, the default variation is used. <para> The evaluator requires either a tenant ID or a
/// user ID to be present in the evaluation context. If  neither is provided, the evaluation will return a result
/// indicating that no variation was selected. </para></remarks>
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
