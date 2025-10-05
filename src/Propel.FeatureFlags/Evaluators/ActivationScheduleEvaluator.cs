using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public sealed class ActivationScheduleEvaluator: EvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.ActivationSchedule;

	public override bool CanProcess(EvaluationOptions flag, EvaluationContext context)
	{
		return flag.ModeSet.Contains([EvaluationMode.Scheduled]);
	}

	public override ValueTask<EvaluationResult?> Evaluate(EvaluationOptions options, EvaluationContext context)
	{
		if (options.Schedule.HasSchedule() == false)
		{
			return new ValueTask<EvaluationResult?>(CreateEvaluationResult(options, 
				context,
				isActive: true,
				because: "Flag has no activation schedule and can be available immediately."));
		}

		var evaluationTime = context.EvaluationTime ?? Knara.UtcStrict.UtcDateTime.UtcNow;
		var (isActive, because) = options.Schedule.IsActiveAt(evaluationTime);

		return new ValueTask<EvaluationResult?>(CreateEvaluationResult(options, context, isActive, because));
	}
}
