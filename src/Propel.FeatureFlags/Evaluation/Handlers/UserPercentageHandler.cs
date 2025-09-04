using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public sealed class UserPercentageHandler: ChainableEvaluationHandler<UserPercentageHandler>, IOrderedEvaluationHandler
{
	public int EvaluationOrder => 3;
	bool IOrderedEvaluationHandler.CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return CanProcess(flag, context);
	}

	Task<EvaluationResult?> IOrderedEvaluationHandler.ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		return ProcessEvaluation(flag, context);
	}

	protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Percentage]) && flag.Users.PercentageEnabled > 0;
	}

	protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (string.IsNullOrWhiteSpace(context.UserId))
		{
			return new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, reason: "User ID required for percentage rollout");
		}

		var (isEnabled, percentage) = flag.Users.IsInUserPercentageRollout(flag.Key, context.UserId);

		return new EvaluationResult(isEnabled: isEnabled,
			variation: isEnabled ? "on" : flag.Variations.DefaultVariation,
			reason: $"User percentage rollout: {percentage}% <= {flag.Users.PercentageEnabled}%");
	}
}
