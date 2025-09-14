using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Strategies;

public sealed class ActivationScheduleEvaluator: IOrderedEvaluator
{
	public EvaluationOrder EvaluationOrder => EvaluationOrder.ActivationSchedule;

	public bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Scheduled]);
	}

	public async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (flag.Schedule.HasSchedule() == false)
		{
			return new EvaluationResult(isEnabled: true, 
				variation: "on", reason: "Flag has no activation schedule and can be available immediately.");
		}

		var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;
		var (isActive, because) = flag.Schedule.IsActiveAt(evaluationTime);

		return new EvaluationResult(isEnabled: isActive, 
			variation: isActive ? "on" : flag.Variations.DefaultVariation, reason: because);
	}
}
