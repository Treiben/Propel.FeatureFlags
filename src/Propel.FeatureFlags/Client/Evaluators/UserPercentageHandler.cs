using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators;

public sealed class UserPercentageHandler: FlagEvaluationHandlerBase<UserPercentageHandler>
{
	protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return flag.Status == FeatureFlagStatus.Percentage;
	}

	protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (string.IsNullOrWhiteSpace(context.UserId))
		{
			return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "User ID required for percentage rollout");
		}

		// Use consistent hashing to ensure same user always gets same result
		var hashInput = $"{flag.Key}:user:{context.UserId}";
		var hash = Hasher.ComputeHash(hashInput);
		var percentage = hash % 100;

		var isEnabled = percentage < flag.PercentageEnabled;

		return new EvaluationResult(isEnabled: isEnabled,
			variation: isEnabled ? "on" : flag.DefaultVariation,
			reason: $"User percentage rollout: {percentage}% < {flag.PercentageEnabled}%");
	}
}
