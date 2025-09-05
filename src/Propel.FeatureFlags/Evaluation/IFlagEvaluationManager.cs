using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace Propel.FeatureFlags.Evaluation;

public interface IFlagEvaluationManager
{
	Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context);
}

public sealed class FlagEvaluationManager : IFlagEvaluationManager
{
	private readonly IOrderedEvaluator[] _handlers;

	public FlagEvaluationManager(HashSet<IOrderedEvaluator> handlers)
	{
		if (handlers == null)
			throw new ArgumentNullException(nameof(handlers));

		// Convert HashSet to ordered array to maintain evaluation order
		_handlers = [.. handlers.OrderBy(h => h.EvaluationOrder)];
	}

	public async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		foreach (var handler in _handlers.Where(h => h.CanProcess(flag, context)))
		{
			var result = await handler.ProcessEvaluation(flag, context);
			if (result != null && result.IsEnabled == false)
			{
				return result;
			}
		}

		var reason = $"All [{flag.EvaluationModeSet}] conditions met for feature flag activation";

		return new EvaluationResult(isEnabled: true, variation: flag.Variations.DefaultVariation, reason: reason);
	}
}
