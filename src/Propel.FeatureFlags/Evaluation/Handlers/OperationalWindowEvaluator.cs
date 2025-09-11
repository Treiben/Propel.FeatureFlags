using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public sealed class OperationalWindowEvaluator : IOrderedEvaluator
{
	public EvaluationOrder EvaluationOrder => EvaluationOrder.OperationalWindow;

	public bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TimeWindow]);
	}

	public async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (flag.OperationalWindow == OperationalWindow.AlwaysOpen)
		{
			// Always open window means the flag is always active
			return new EvaluationResult(isEnabled: true, variation: "on", reason: "Flag operational window is always open.");
		}

		// Convert to specified timezone
		var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;

		var (isActive, because) = flag.OperationalWindow.IsActiveAt(evaluationTime, flag.OperationalWindow.TimeZone);

		return new EvaluationResult(isEnabled: isActive, variation: isActive ? "on" : flag.Variations.DefaultVariation, reason: because);
	}
}
