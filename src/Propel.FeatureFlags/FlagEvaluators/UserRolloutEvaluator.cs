using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public sealed class UserRolloutEvaluator: OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.UserRollout;

	public override bool CanProcess(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserRolloutPercentage, EvaluationMode.UserTargeted]) 
			|| flag.UserAccessControl.HasAccessRestrictions();
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		if (string.IsNullOrWhiteSpace(context.UserId))
		{
			throw new InvalidOperationException("User ID is required for percentage rollout evaluation.");
		}

		var (result, because) = flag.UserAccessControl.EvaluateAccess(context.UserId!, flag.Identifier.Key);
		var isEnabled = result == AccessResult.Allowed;

		return CreateEvaluationResult(flag, context, isEnabled, because);
	}
}
