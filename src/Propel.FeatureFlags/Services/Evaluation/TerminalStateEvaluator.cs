using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Services.Evaluation;

public sealed class TerminalStateEvaluator : OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.Terminal;

	public override bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		// Handle fundamental states that don't require complex logic
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled, EvaluationMode.Enabled]);
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		bool disabled = flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]);
		string because = disabled
			? $"Feature flag '{flag.Key}' is explicitly disabled"
			: $"Feature flag '{flag.Key}' is explicitly enabled";

		return CreateEvaluationResult(flag, context, !disabled, because);
	}
}