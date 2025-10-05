using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace Propel.FeatureFlags.Clients;

public interface IEvaluators
{
	ValueTask<EvaluationResult?> Evaluate(EvaluationOptions flag, EvaluationContext context);
}

public sealed class AllEvaluators : IEvaluators
{
	private readonly IOptionsEvaluator[] _evaluators;

	public AllEvaluators(HashSet<IOptionsEvaluator> handlers)
	{
		if (handlers == null)
			throw new ArgumentNullException(nameof(handlers));

		// Convert HashSet to ordered array to maintain evaluation order
		_evaluators = [.. handlers.OrderBy(h => h.EvaluationOrder)];
	}

	public async ValueTask<EvaluationResult?> Evaluate(EvaluationOptions evaluationOptions, EvaluationContext context)
	{
		EvaluationResult? result = default;

		var evaluators = _evaluators
				.Where(h => h.CanProcess(evaluationOptions, context))
				.OrderBy(p => p.EvaluationOrder)
				.ToList();

		foreach (var evaluator in evaluators)
		{
			result = await evaluator.Evaluate(evaluationOptions, context);
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

public static class DefaultEvaluators
{
	public static IEvaluators Create()
	{
		// Create processor with all evaluators for legacy applications that don't use DI
		return new AllEvaluators(new HashSet<IOptionsEvaluator>(
		[
			new ActivationScheduleEvaluator(),
			new OperationalWindowEvaluator(),
			new TargetingRulesEvaluator(),
			new TenantRolloutEvaluator(),
			new TerminalStateEvaluator(),
			new UserRolloutEvaluator(),
		]));
	}
}
