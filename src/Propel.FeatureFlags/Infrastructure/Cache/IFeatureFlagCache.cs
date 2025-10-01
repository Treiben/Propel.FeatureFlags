using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.Cache;

public interface IFeatureFlagCache
{
	Task<EvaluationOptions?> GetAsync(CacheKey key, CancellationToken cancellationToken = default);
	Task SetAsync(CacheKey key, EvaluationOptions flag, CancellationToken cancellationToken = default);
	Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);
	Task ClearAsync(CancellationToken cancellationToken = default);
}
