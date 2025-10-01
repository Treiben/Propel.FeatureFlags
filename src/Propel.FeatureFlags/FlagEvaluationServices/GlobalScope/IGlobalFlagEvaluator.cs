using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluationServices;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace Propel.FeatureFlags.Services.GlobalScope;

public interface IGlobalFlagEvaluator
{
	Task<EvaluationResult?> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default);
}

public class GlobalFlagEvaluator(
	IFeatureFlagRepository repository,
	IFlagEvaluationManager evaluationManager,
	IFeatureFlagCache? cache = null) : IGlobalFlagEvaluator
{
	private readonly IFeatureFlagRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly IFlagEvaluationManager _evaluationManager = evaluationManager ?? throw new ArgumentNullException(nameof(evaluationManager));

	public async Task<EvaluationResult?> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		var flagConfig = await GetFlagConfiguration(flagKey, cancellationToken);
		if (flagConfig == null)
		{
			throw new Exception("The global feature flag is not found. Please create the flag in the system before evaluating it or remove reference to it.");
		}

		return await _evaluationManager.ProcessEvaluation(flagConfig, context);
	}

	private async Task<EvaluationOptions?> GetFlagConfiguration(string flagKey, CancellationToken cancellationToken)
	{
		// Create composite key for uniqueness per application
		var cacheKey = new GlobalCacheKey(flagKey);
		// Try cache first
		EvaluationOptions? flagConfig = null;
		if (cache != null)
		{
			flagConfig = await cache.GetAsync(cacheKey, cancellationToken);
		}

		// If not in cache, get from repository
		if (flagConfig == null)
		{
			flagConfig = await _repository.GetEvaluationOptionsAsync(new FlagIdentifier(
					key: flagKey,
					scope: Scope.Global
				), cancellationToken);

			// Cache for future requests if found
			if (flagConfig != null && cache != null)
			{
				await cache.SetAsync(cacheKey, flagConfig, cancellationToken);
			}
		}

		return flagConfig;
	}
}
