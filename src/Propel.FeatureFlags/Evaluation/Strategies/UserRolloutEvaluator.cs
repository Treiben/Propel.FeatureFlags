using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Strategies;

public sealed class UserRolloutEvaluator: IOrderedEvaluator
{
	public EvaluationOrder EvaluationOrder => EvaluationOrder.UserRollout;

	public bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserRolloutPercentage, EvaluationMode.UserTargeted]) 
			|| flag.UserAccessControl.HasAccessRestrictions();
	}

	public async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (string.IsNullOrWhiteSpace(context.UserId))
		{
			throw new InvalidOperationException("User ID is required for percentage rollout evaluation.");
		}

		var (result, because) = flag.UserAccessControl.EvaluateAccess(context.UserId!, flag.Key);
		var isEnabled = result == AccessResult.Allowed;

		if (isEnabled == false)
		{
			return new EvaluationResult(
				isEnabled: false,
				variation: flag.Variations.DefaultVariation,
				reason: because);
		}

		// For enabled users, determine which variation they should get
		var selectedVariation = flag.Variations.SelectVariationFor(flag.Key, context.UserId!);

		return new EvaluationResult(
			isEnabled: true,
			variation: selectedVariation,
			reason: because);
	}
}
