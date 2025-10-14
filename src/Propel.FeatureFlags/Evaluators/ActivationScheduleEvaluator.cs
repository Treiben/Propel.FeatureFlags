using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

/// <summary>
/// Evaluates whether a feature flag is active based on its activation schedule.
/// </summary>
/// <remarks>This evaluator determines the activation status of a feature flag by checking its associated schedule
/// against the current evaluation time. If no activation schedule is defined, the flag is considered immediately
/// active.</remarks>
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
