using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators;

public sealed class StatusBasedFlagHandler : ChainableEvaluationHandler<StatusBasedFlagHandler>, IOrderedEvaluationHandler
{
	public int EvaluationOrder => 7;

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
		// Handle basic mode types that don't require complex logic
		return flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Disabled, FlagEvaluationMode.Enabled]);
	}

	protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Disabled]))
			return new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, reason: "Flag disabled");

		return new EvaluationResult(isEnabled: true, variation: "on", reason: "Flag enabled");
	}
}