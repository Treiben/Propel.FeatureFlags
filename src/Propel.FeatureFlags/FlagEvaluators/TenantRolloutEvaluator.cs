using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace Propel.FeatureFlags.Evaluation;

public sealed class TenantRolloutEvaluator : OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.TenantRollout;

	public override bool CanProcess(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TenantRolloutPercentage, EvaluationMode.TenantTargeted]) 
			|| flag.TenantAccessControl.HasAccessRestrictions();
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		if (string.IsNullOrWhiteSpace(context.TenantId))
		{
			throw new InvalidOperationException("Tenant ID is required for percentage rollout evaluation.");
		}

		var (result, because) = flag.TenantAccessControl.EvaluateAccess(context.TenantId!, flag.Identifier.Key);
		var isEnabled = result == AccessResult.Allowed;

		return CreateEvaluationResult(flag, context, isEnabled, because);
	}
}
