using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace Propel.FeatureFlags.Services;

public interface IFlagEvaluationManager
{
	Task<EvaluationResult?> ProcessEvaluation(FlagEvaluationConfiguration flag, EvaluationContext context);
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

	public async Task<EvaluationResult?> ProcessEvaluation(FlagEvaluationConfiguration flag, EvaluationContext context)
	{
		var processingHandlers = _handlers.Where(h => h.CanProcess(flag, context)).ToList();
		EvaluationResult? result = default;
		foreach (var handler in processingHandlers)
		{
			result = await handler.ProcessEvaluation(flag, context);
			if (result != null && result.IsEnabled == false)
			{
				return result;
			}
		}

		if (result != default)
		{
			if (processingHandlers.Count == 1)
				return result;

			// If multiple handlers processed, and final result is enabled, provide a combined reason
			var reason = $"All [{flag.ActiveEvaluationModes}] conditions met for feature flag activation";
			return new EvaluationResult(isEnabled: true, variation: result.Variation, reason: reason);
		}

		return result;
	}
}
