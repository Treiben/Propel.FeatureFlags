using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators
{
	public sealed class TargetedFlagHandler : FlagEvaluationHandlerBase<TargetedFlagHandler>
	{
		protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
		{
			return flag.Status == FeatureFlagStatus.UserTargeted;
		}

		protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
		{
			// Build enriched attributes that include tenant and user context
			var enrichedAttributes = new Dictionary<string, object>(context.Attributes);
			if (!string.IsNullOrWhiteSpace(context.TenantId))
			{
				enrichedAttributes["tenantId"] = context.TenantId;
			}
			if (!string.IsNullOrWhiteSpace(context.UserId))
			{
				enrichedAttributes["userId"] = context.UserId;
			}

			// Evaluate targeting rules
			foreach (var rule in flag.TargetingRules)
			{
				if (EvaluateTargetingRule(rule, enrichedAttributes))
				{
					return new EvaluationResult(isEnabled: true, variation: rule.Variation, reason: $"Targeting rule matched: {rule.Attribute} {rule.Operator} {string.Join(",", rule.Values)}");
				}
			}

			return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "No targeting rules matched");
		}

		private bool EvaluateTargetingRule(TargetingRule rule, Dictionary<string, object> attributes)
		{
			if (!attributes.TryGetValue(rule.Attribute, out var attributeValue))
			{
				return false;
			}

			var stringValue = attributeValue?.ToString() ?? string.Empty;
			var result = rule.Operator switch
			{
				TargetingOperator.Equals => rule.Values.Contains(stringValue, StringComparer.OrdinalIgnoreCase),
				TargetingOperator.NotEquals => !rule.Values.Contains(stringValue, StringComparer.OrdinalIgnoreCase),
				TargetingOperator.Contains => rule.Values.Any(v => stringValue.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0),
				TargetingOperator.NotContains => rule.Values.All(v => stringValue.IndexOf(v, StringComparison.OrdinalIgnoreCase) < 0),
				TargetingOperator.In => rule.Values.Contains(stringValue, StringComparer.OrdinalIgnoreCase),
				TargetingOperator.NotIn => !rule.Values.Contains(stringValue, StringComparer.OrdinalIgnoreCase),
				TargetingOperator.GreaterThan => double.TryParse(stringValue, out var numValue) &&
											   rule.Values.Any(v => double.TryParse(v, out var ruleValue) && numValue > ruleValue),
				TargetingOperator.LessThan => double.TryParse(stringValue, out var numValue2) &&
											rule.Values.Any(v => double.TryParse(v, out var ruleValue) && numValue2 < ruleValue),
				_ => false
			};

			return result;
		}
	}
}
