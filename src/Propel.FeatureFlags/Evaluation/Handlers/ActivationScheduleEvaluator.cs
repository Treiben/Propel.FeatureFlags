using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public sealed class ActivationScheduleEvaluator: IOrderedEvaluator
{
	public EvaluationOrder EvaluationOrder => EvaluationOrder.ActivationSchedule;

	public bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Scheduled]);
	}

	public async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (flag.Schedule.HasSchedule() == false)
		{
			throw new InvalidOperationException("The flag's activation schedule is not setup.");
		}

		var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;
		var (isActive, because) = flag.Schedule.IsActiveAt(evaluationTime);

		return new EvaluationResult(isEnabled: isActive, 
			variation: isActive ? "on" : flag.Variations.DefaultVariation, reason: because);
	}
}
