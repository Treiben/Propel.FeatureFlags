using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace Propel.FeatureFlags.Clients;

public interface IFeatureFlagProcessor
{
	ValueTask<EvaluationResult?> ProcessEvaluation(EvaluationOptions flag, EvaluationContext context);
}

public sealed class FeatureFlagProcessor : IFeatureFlagProcessor
{
	private readonly IOptionsEvaluator[] _evaluators;

	public FeatureFlagProcessor(HashSet<IOptionsEvaluator> handlers)
	{
		if (handlers == null)
			throw new ArgumentNullException(nameof(handlers));

		// Convert HashSet to ordered array to maintain evaluation order
		_evaluators = [.. handlers.OrderBy(h => h.EvaluationOrder)];
	}

	public async ValueTask<EvaluationResult?> ProcessEvaluation(EvaluationOptions flag, EvaluationContext context)
	{
		EvaluationResult? result = default;

		var evaluators = _evaluators
				.Where(h => h.CanProcess(flag, context))
				.OrderBy(p => p.EvaluationOrder)
				.ToList();

		foreach (var evaluator in evaluators)
		{
			result = await evaluator.Evaluate(flag, context);
			if (result != null && result.IsEnabled == false)
			{
				return result;
			}
		}

		if (result != default)
		{
			if (evaluators.Count == 1)
				return result;

			// If multiple handlers processed, and final result is enabled, provide a combined reason
			var reason = $"All configured conditions met for feature flag activation";
			return new EvaluationResult(isEnabled: true, variation: result.Variation, reason: reason);
		}

		return result;
	}
}
