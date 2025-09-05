using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public sealed class StatusBasedFlagHandler : IOrderedEvaluator
{
	public EvaluationOrder EvaluationOrder => EvaluationOrder.;

	public bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		// Handle basic mode types that don't require complex logic
		return flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Disabled, FlagEvaluationMode.Enabled]);
	}

	public async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Disabled]))
			return new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, reason: "Flag disabled");

		return new EvaluationResult(isEnabled: true, variation: "on", reason: "Flag enabled");
	}
}