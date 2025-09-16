using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.Cache;

public interface IFeatureFlagCache
{
	Task<EvaluationCriteria?> GetAsync(CacheKey key, CancellationToken cancellationToken = default);
	Task SetAsync(CacheKey key, EvaluationCriteria flag, CancellationToken cancellationToken = default);
	Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);
	Task ClearAsync(CancellationToken cancellationToken = default);
}
