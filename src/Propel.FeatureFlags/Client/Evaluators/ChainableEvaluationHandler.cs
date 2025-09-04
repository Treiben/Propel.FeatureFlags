using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators;

public interface IChainableEvaluationHandler
{
	IChainableEvaluationHandler? NextHandler { get; set; }
	Task<EvaluationResult?> Handle(FeatureFlag flag, EvaluationContext context);
}

public abstract class ChainableEvaluationHandler<T> : IChainableEvaluationHandler where T : class
{
	public IChainableEvaluationHandler? NextHandler { get; set; }

	public async Task<EvaluationResult?> Handle(FeatureFlag flag, EvaluationContext context)
	{
		// Check if this evaluator should handle this flag
		if (!CanProcess(flag, context))
		{
			return await CallNext(flag, context);
		}

		// Handle the evaluation
		var result = await ProcessEvaluation(flag, context);

		// If result is inconclusive and we have a next evaluator, continue the chain
		if (result == null && NextHandler != null)
		{
			return await NextHandler.Handle(flag, context);
		}

		// Return result or default fallback
		return result ?? new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, 
			reason: "No evaluator could handle this flag");
	}

	protected virtual async Task<EvaluationResult> CallNext(FeatureFlag flag, EvaluationContext context)
	{
		if (NextHandler != null)
		{
			return await NextHandler.Handle(flag, context);
		}

		return new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, 
			reason: "End of evaluation chain");
	}

	protected abstract bool CanProcess(FeatureFlag flag, EvaluationContext context);
	protected abstract Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context);
}
