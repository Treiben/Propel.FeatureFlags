using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

/// <summary>
/// Evaluates whether a feature flag is active based on the configured operational time window.
/// </summary>
/// <remarks>This evaluator determines the activation status of a feature flag by checking if the current time
/// falls within the specified operational window. The operational window can be configured to always allow activation
/// or to restrict activation to specific time periods.</remarks>
public sealed class OperationalWindowEvaluator : EvaluatorBase
{
	public override EvaluationOrder EvaluationOrder => EvaluationOrder.OperationalWindow;

	public override bool CanProcess(EvaluationOptions options, EvaluationContext context)
	{
		return options.ModeSet.Contains([EvaluationMode.TimeWindow]);
	}

	public override ValueTask<EvaluationResult?> Evaluate(EvaluationOptions options, EvaluationContext context)
	{
		if (options.OperationalWindow == UtcTimeWindow.AlwaysOpen)
		{
			// Always open window means the flag is always active
			return new ValueTask<EvaluationResult?>(CreateEvaluationResult(options, 
				context,
				isActive: true, 
				because: "Flag operational window is always open."));
		}

		// Convert to specified timezone
		var evaluationTime = context.EvaluationTime ?? UtcDateTime.UtcNow;

		var (isActive, because) = options.OperationalWindow.IsActiveAt(evaluationTime);

		return new ValueTask<EvaluationResult?>(CreateEvaluationResult(options, context, isActive, because));
	}
}
