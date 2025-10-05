using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public sealed class TerminalStateEvaluator : EvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.Terminal;

	public override bool CanProcess(EvaluationOptions options, EvaluationContext context)
	{
		// Handle fundamental states that don't require complex logic
		return options.ModeSet.Contains([EvaluationMode.Off, EvaluationMode.On]);
	}

	public override ValueTask<EvaluationResult?> Evaluate(EvaluationOptions options, EvaluationContext context)
	{
		bool disabled = options.ModeSet.Contains([EvaluationMode.Off]);
		string because = disabled
			? $"Feature flag '{options.Key}' is explicitly disabled"
			: $"Feature flag '{options.Key}' is explicitly enabled";

		return new ValueTask<EvaluationResult?>(CreateEvaluationResult(options, context, !disabled, because));
	}
}