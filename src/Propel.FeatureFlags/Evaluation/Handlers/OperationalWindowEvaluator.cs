using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public sealed class OperationalWindowEvaluator : IOrderedEvaluator
{
	public EvaluationOrder EvaluationOrder => EvaluationOrder.OperationalWindow;

	public bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.TimeWindow]);
	}

	public async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (!flag.OperationalWindow.HasWindow())
		{
			throw new InvalidOperationException("The flag's operational time window is not setup.");
		}

		// Convert to specified timezone
		var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;

		var (isActive, because) = flag.OperationalWindow.IsActiveAt(evaluationTime, context.TimeZone);

		return new EvaluationResult(isEnabled: isActive, variation: isActive ? "on" : flag.Variations.DefaultVariation, reason: because);
	}
}
