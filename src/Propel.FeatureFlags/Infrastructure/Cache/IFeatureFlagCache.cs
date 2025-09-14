using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.Cache;

public interface IFeatureFlagCache
{
	Task<FeatureFlag?> GetAsync(CacheKey key, CancellationToken cancellationToken = default);
	Task SetAsync(CacheKey key, FeatureFlag flag, CancellationToken cancellationToken = default);
	Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);
	Task ClearAsync(CancellationToken cancellationToken = default);
}
