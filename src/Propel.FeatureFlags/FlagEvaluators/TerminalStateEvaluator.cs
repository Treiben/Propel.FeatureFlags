using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public sealed class TerminalStateEvaluator : OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.Terminal;

	public override bool CanProcess(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		// Handle fundamental states that don't require complex logic
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Off, EvaluationMode.On]);
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		bool disabled = flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Off]);
		string because = disabled
			? $"Feature flag '{flag.Identifier.Key}' is explicitly disabled"
			: $"Feature flag '{flag.Identifier.Key}' is explicitly enabled";

		return CreateEvaluationResult(flag, context, !disabled, because);
	}
}