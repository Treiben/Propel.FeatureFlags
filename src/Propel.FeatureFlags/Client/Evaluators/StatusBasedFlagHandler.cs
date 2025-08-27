using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators
{
	public sealed class StatusBasedFlagHandler : FlagEvaluationHandlerBase<StatusBasedFlagHandler>
	{
		protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
		{
			// Handle basic status types that don't require complex logic
			return flag.Status is FeatureFlagStatus.Disabled or FeatureFlagStatus.Enabled;
		}

		protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
		{
			return flag.Status switch
			{
				FeatureFlagStatus.Disabled => new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Flag disabled"),
				FeatureFlagStatus.Enabled => new EvaluationResult(isEnabled: true, variation: "on", reason: "Flag enabled"),
				_ => new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Unknown flag status")
			};
		}
	}
}