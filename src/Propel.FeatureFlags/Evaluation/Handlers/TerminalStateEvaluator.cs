using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public sealed class TerminalStateEvaluator : IOrderedEvaluator
{
	public EvaluationOrder EvaluationOrder => EvaluationOrder.Terminal;

	public bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		// Handle fundamental states that don't require complex logic
		return flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Disabled, FlagEvaluationMode.Enabled]);
	}

	public async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Disabled]))
		{
			return new EvaluationResult(
				isEnabled: false, 
				variation: flag.Variations?.DefaultVariation ?? "off", 
				reason: $"Feature flag '{flag.Key}' is explicitly disabled");
		}

		return new EvaluationResult(
			isEnabled: true, 
			variation: "on", 
			reason: $"Feature flag '{flag.Key}' is explicitly enabled");
	}
}