using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public sealed class OperationalWindowEvaluator : OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.OperationalWindow;

	public override bool CanProcess(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		return flag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TimeWindow]);
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		if (flag.OperationalWindow == UtcTimeWindow.AlwaysOpen)
		{
			// Always open window means the flag is always active
			return CreateEvaluationResult(flag, 
				context,
				isActive: true, 
				because: "Flag operational window is always open.");
		}

		// Convert to specified timezone
		var evaluationTime = context.EvaluationTime ?? UtcDateTime.UtcNow;

		var (isActive, because) = flag.OperationalWindow.IsActiveAt(evaluationTime);

		return CreateEvaluationResult(flag, context, isActive, because);
	}
}
