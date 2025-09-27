using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public sealed class ActivationScheduleEvaluator: OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.ActivationSchedule;

	public override bool CanProcess(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Scheduled]);
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		if (flag.Schedule.HasSchedule() == false)
		{
			return CreateEvaluationResult(flag, 
				context,
				isActive: true,
				because: "Flag has no activation schedule and can be available immediately.");
		}

		var evaluationTime = context.EvaluationTime ?? Knara.UtcStrict.UtcDateTime.UtcNow;
		var (isActive, because) = flag.Schedule.IsActiveAt(evaluationTime);

		return CreateEvaluationResult(flag, context, isActive, because);
	}
}
