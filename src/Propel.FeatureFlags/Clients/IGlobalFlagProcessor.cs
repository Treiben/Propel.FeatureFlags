using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace Propel.FeatureFlags.Clients;

public interface IGlobalFlagProcessor
{
	Task<EvaluationResult?> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default);
}

public sealed class GlobalFlagProcessor(
	IFeatureFlagRepository repository,
	IEvaluatorsSet evaluators,
	IFeatureFlagCache? cache = null) : IGlobalFlagProcessor
{
	private readonly IFeatureFlagRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly IEvaluatorsSet _evaluators = evaluators ?? throw new ArgumentNullException(nameof(evaluators));

	public async Task<EvaluationResult?> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		var flagConfig = await GetFlagConfiguration(flagKey, cancellationToken);
		if (flagConfig == null)
		{
			throw new Exception("The global feature flag is not found. Please create the flag in the system before evaluating it or remove reference to it.");
		}

		return await _evaluators.Evaluate(flagConfig, context);
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
