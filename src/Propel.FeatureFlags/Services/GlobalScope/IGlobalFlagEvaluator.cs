using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace Propel.FeatureFlags.Services.GlobalScope;

public interface IGlobalFlagEvaluator
{
	Task<EvaluationResult?> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default);
}

public class GlobalFlagEvaluator(
	IFlagEvaluationRepository repository,
	IFlagEvaluationManager evaluationManager,
	IFeatureFlagCache? cache = null) : IGlobalFlagEvaluator
{
	private readonly IFlagEvaluationRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly IFlagEvaluationManager _evaluationManager = evaluationManager ?? throw new ArgumentNullException(nameof(evaluationManager));

	public async Task<EvaluationResult?> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		var flagData = await GetFlagAsync(flagKey, cancellationToken);
		if (flagData == null)
		{
			throw new Exception("The global feature flag is not found. Please create the flag in the system before evaluating it or remove reference to it.");
		}

		return await _evaluationManager.ProcessEvaluation(flagData, context);
	}

	private async Task<EvaluationCriteria?> GetFlagAsync(string flagKey, CancellationToken cancellationToken)
	{
		// Create composite key for uniqueness per application
		var cacheKey = new GlobalCacheKey(flagKey);
		// Try cache first
		EvaluationCriteria? flagData = null;
		if (cache != null)
		{
			flagData = await cache.GetAsync(cacheKey, cancellationToken);
		}

		// If not in cache, get from repository
		if (flagData == null)
		{
			flagData = await _repository.GetAsync(new FlagKey(
					key: flagKey,
					scope: Scope.Global
				), cancellationToken);

			// Cache for future requests if found
			if (flagData != null && cache != null)
			{
				await cache.SetAsync(cacheKey, flagData, cancellationToken);
			}
		}

		return flagData;
	}
}
