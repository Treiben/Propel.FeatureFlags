using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public sealed class TenantRolloutEvaluator : EvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.TenantRollout;

	public override bool CanProcess(EvaluationOptions options, EvaluationContext context)
	{
		return options.ModeSet.Contains([EvaluationMode.TenantRolloutPercentage, EvaluationMode.TenantTargeted]) 
			|| options.TenantAccessControl.HasAccessRestrictions();
	}

	public override ValueTask<EvaluationResult?> Evaluate(EvaluationOptions options, EvaluationContext context)
	{
		if (string.IsNullOrWhiteSpace(context.TenantId))
		{
			throw new EvaluationOptionsArgumentException(nameof(context.TenantId), "Tenant ID is required for percentage rollout evaluation.");
		}

		var (result, because) = options.TenantAccessControl.EvaluateAccess(context.TenantId!, options.Key);
		var isEnabled = result == AccessResult.Allowed;

		return new ValueTask<EvaluationResult?>(CreateEvaluationResult(options, context, isEnabled, because));
	}
}
