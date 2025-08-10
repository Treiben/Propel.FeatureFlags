using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators
{
	public sealed class TenantOverrideHandler: FlagEvaluationHandlerBase<TenantOverrideHandler>
	{
		protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
		{
			// Always check tenant overrides first if tenant ID is provided
			return !string.IsNullOrEmpty(context.TenantId);
		}

		protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
		{
			var tenantId = context.TenantId!;
			// If no tenant ID, skip tenant evaluation
			if (string.IsNullOrEmpty(tenantId))
			{
				return null; // No tenant evaluation needed
			}

			// Check explicit tenant overrides first
			if (flag.DisabledTenants.Contains(tenantId))
			{
				return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Tenant explicitly disabled");
			}

			if (flag.EnabledTenants.Contains(tenantId))
			{
				// Continue to user-level evaluation - tenant is allowed
				return null;
			}
			// Check tenant-level percentage rollout
			var tenantAllowed = EvaluateTenantPercentage(flag, tenantId);
			if (!tenantAllowed)
			{
				return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Tenant not in percentage rollout");
			}
			// Continue to user-level evaluation - tenant is allowed
			return null;
		}

		private bool EvaluateTenantPercentage(FeatureFlag flag, string tenantId)
		{
			if (flag.TenantPercentageEnabled <= 0)
			{
				return false;
			}

			if (flag.TenantPercentageEnabled >= 100)
			{
				return true;
			}

			// Use consistent hashing to ensure same tenant always gets same result
			var hashInput = $"{flag.Key}:tenant:{tenantId}";
			var hash = Hasher.ComputeHash(hashInput);
			var percentage = hash % 100;
			var isAllowed = percentage < flag.TenantPercentageEnabled;

			return isAllowed;
		}
	}
}
