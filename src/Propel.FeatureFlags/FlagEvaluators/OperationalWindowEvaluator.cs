using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace Propel.FeatureFlags.Evaluation;

public sealed class OperationalWindowEvaluator : OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.OperationalWindow;

	public override bool CanProcess(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TimeWindow]);
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		if (flag.OperationalWindow == OperationalWindow.AlwaysOpen)
		{
			// Always open window means the flag is always active
			return CreateEvaluationResult(flag, 
				context,
				isActive: true, 
				because: "Flag operational window is always open.");
		}

		// Convert to specified timezone
		var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;
		var evaluationTimeZone = context.TimeZone ?? flag.OperationalWindow.TimeZone;

		var (isActive, because) = flag.OperationalWindow.IsActiveAt(evaluationTime, evaluationTimeZone);

		return CreateEvaluationResult(flag, context, isActive, because);
	}
}
