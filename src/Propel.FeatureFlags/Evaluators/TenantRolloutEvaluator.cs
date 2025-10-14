using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

/// <summary>
/// Evaluates whether a tenant satisfies the conditions for a rollout based on percentage or targeted access control.
/// </summary>
/// <remarks>This evaluator processes tenant-specific rollout conditions, including percentage-based rollouts and
/// targeted tenant access restrictions. It determines whether a tenant is eligible for a feature or configuration based
/// on the provided evaluation options and context.</remarks>
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
