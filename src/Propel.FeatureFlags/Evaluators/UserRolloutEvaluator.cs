using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public sealed class UserRolloutEvaluator: EvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.UserRollout;

	public override bool CanProcess(EvaluationOptions options, EvaluationContext context)
	{
		return options.ModeSet.Contains([EvaluationMode.UserRolloutPercentage, EvaluationMode.UserTargeted]) 
			|| options.UserAccessControl.HasAccessRestrictions();
	}

	public override ValueTask<EvaluationResult?> Evaluate(EvaluationOptions options, EvaluationContext context)
	{
		if (string.IsNullOrWhiteSpace(context.UserId))
		{
			throw new EvaluationOptionsArgumentException(nameof(context.UserId), "User ID is required for percentage rollout evaluation.");
		}

		var (result, because) = options.UserAccessControl.EvaluateAccess(context.UserId!, options.Key);
		var isEnabled = result == AccessResult.Allowed;

		return new ValueTask<EvaluationResult?>(CreateEvaluationResult(options, context, isEnabled, because));
	}
}
