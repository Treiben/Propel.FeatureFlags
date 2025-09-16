using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Services.Evaluation;

public sealed class ActivationScheduleEvaluator: OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.ActivationSchedule;

	public override bool CanProcess(EvaluationCriteria flag, EvaluationContext context)
	{
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Scheduled]);
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(EvaluationCriteria flag, EvaluationContext context)
	{
		if (flag.Schedule.HasSchedule() == false)
		{
			return CreateEvaluationResult(flag, 
				context,
				isActive: true,
				because: "Flag has no activation schedule and can be available immediately.");
		}

		var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;
		var (isActive, because) = flag.Schedule.IsActiveAt(evaluationTime);

		return CreateEvaluationResult(flag, context, isActive, because);
	}
}
