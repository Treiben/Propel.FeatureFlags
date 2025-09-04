using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public sealed class ExpirationDateHandler : ChainableEvaluationHandler<ExpirationDateHandler>, IOrderedEvaluationHandler
{
	public int EvaluationOrder => 0;

	bool IOrderedEvaluationHandler.CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return CanProcess(flag, context);
	}

	Task<EvaluationResult?> IOrderedEvaluationHandler.ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		return ProcessEvaluation(flag, context);
	}

	protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;
		return flag.ExpirationDate.HasValue && flag.ExpirationDate.Value != default && evaluationTime > flag.ExpirationDate.Value;
	}

	protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;

		return new EvaluationResult(isEnabled: true, variation: flag.Variations.DefaultVariation,
			reason: $"Flag {flag.Key} expired at {flag.ExpirationDate!.Value}, current time {evaluationTime} but it still might be used in the code base.");
	}
}
