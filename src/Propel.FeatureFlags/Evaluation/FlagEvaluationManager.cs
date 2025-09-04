using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace Propel.FeatureFlags.Evaluation;

public sealed class FlagEvaluationManager
{
	private readonly List<IOrderedEvaluationHandler> _handlers;

	public FlagEvaluationManager(List<IOrderedEvaluationHandler> handlers)
	{
		_handlers = handlers?.OrderBy(h => h.EvaluationOrder).ToList()
						?? throw new ArgumentNullException(nameof(handlers));
	}

	public bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return _handlers.Any(h => h.CanProcess(flag, context));
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
