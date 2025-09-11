using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public sealed class TenantRolloutEvaluator : IOrderedEvaluator
{
	public EvaluationOrder EvaluationOrder => EvaluationOrder.TenantRollout;

	public bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		var mustEvaluateTenant = !string.IsNullOrWhiteSpace(context.TenantId)
			&& flag.TenantAccess.IsTenantExplicitlyManaged(context.TenantId!);

		return mustEvaluateTenant
			|| (flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.TenantRolloutPercentage]) && flag.TenantAccess.HasAccessRestrictions());
	}

	public async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (string.IsNullOrWhiteSpace(context.TenantId))
		{
			throw new InvalidOperationException("Tenant ID is required for percentage rollout evaluation.");
		}

		var (result, because) = flag.TenantAccess.EvaluateTenantAccess(context.TenantId!, flag.Key);
		var isEnabled = result == TenantAccessResult.Allowed;

		if (!isEnabled)
		{
			return new EvaluationResult(
				isEnabled: false,
				variation: flag.Variations.DefaultVariation,
				reason: because);
		}

		// For enabled tenants, determine which variation they should get
		var selectedVariation = flag.Variations.SelectVariationFor(flag.Key, context.TenantId!);

		return new EvaluationResult(
			isEnabled: true,
			variation: selectedVariation,
			reason: because);
	}
}
