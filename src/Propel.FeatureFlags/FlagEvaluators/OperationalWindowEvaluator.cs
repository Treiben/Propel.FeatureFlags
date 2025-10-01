using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public sealed class OperationalWindowEvaluator : OrderedEvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.OperationalWindow;

	public override bool CanProcess(EvaluationOptions options, EvaluationContext context)
	{
		return options.ModeSet.Contains([EvaluationMode.TimeWindow]);
	}

	public override async Task<EvaluationResult?> ProcessEvaluation(EvaluationOptions options, EvaluationContext context)
	{
		if (options.OperationalWindow == UtcTimeWindow.AlwaysOpen)
		{
			// Always open window means the flag is always active
			return CreateEvaluationResult(options, 
				context,
				isActive: true, 
				because: "Flag operational window is always open.");
		}

		// Convert to specified timezone
		var evaluationTime = context.EvaluationTime ?? UtcDateTime.UtcNow;

		var (isActive, because) = options.OperationalWindow.IsActiveAt(evaluationTime);

		return CreateEvaluationResult(options, context, isActive, because);
	}
}
