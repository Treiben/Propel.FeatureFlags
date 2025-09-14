using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Cache
{
	public interface IFeatureFlagCache
	{
		Task<FeatureFlag?> GetAsync(CacheKey key, CancellationToken cancellationToken = default);
		Task SetAsync(CacheKey key, FeatureFlag flag, TimeSpan? expiry, CancellationToken cancellationToken = default);
		Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);
		Task ClearAsync(CancellationToken cancellationToken = default);
	}
}
