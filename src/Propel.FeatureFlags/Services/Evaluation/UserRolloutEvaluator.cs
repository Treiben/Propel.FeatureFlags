using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Services.Evaluation;

public sealed class UserRolloutEvaluator: OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.UserRollout;

	public override bool CanProcess(EvaluationCriteria flag, EvaluationContext context)
	{
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserRolloutPercentage, EvaluationMode.UserTargeted]) 
			|| flag.UserAccessControl.HasAccessRestrictions();
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(EvaluationCriteria flag, EvaluationContext context)
	{
		if (string.IsNullOrWhiteSpace(context.UserId))
		{
			throw new InvalidOperationException("User ID is required for percentage rollout evaluation.");
		}

		var (result, because) = flag.UserAccessControl.EvaluateAccess(context.UserId!, flag.FlagKey);
		var isEnabled = result == AccessResult.Allowed;

		return CreateEvaluationResult(flag, context, isEnabled, because);
	}
}
