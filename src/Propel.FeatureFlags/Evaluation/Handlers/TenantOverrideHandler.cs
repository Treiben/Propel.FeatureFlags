using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public sealed class TenantOverrideHandler: ChainableEvaluationHandler<TenantOverrideHandler>, IOrderedEvaluationHandler
{
	public int EvaluationOrder => 1;

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
		// Always check tenant overrides first if tenant ID is provided
		return !string.IsNullOrWhiteSpace(context.TenantId);
	}

	protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		var tenantId = context.TenantId!;
		if (!flag.Tenants.IsTenantExplicitlySet(tenantId))
		{
			// Check tenant-level percentage rollout
			var tenantAllowed = flag.Tenants.IsInTenantPercentageRollout(flag.Key, tenantId);
			if (!tenantAllowed)
			{
				return new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, reason: "Tenant not in percentage rollout");
			}
		}

		// Check explicit tenant overrides
		if (!flag.Tenants.IsTenantEnabled(tenantId))
		{
			return new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, reason: "Tenant explicitly disabled");
		}

		// Continue to user-level evaluation - tenant is allowed
		return null;
	}
}
