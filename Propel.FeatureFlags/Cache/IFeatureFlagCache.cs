using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Cache;

public interface IFeatureFlagCache
{
	Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default);
	Task SetAsync(string key, FeatureFlag flag, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
	Task RemoveAsync(string key, CancellationToken cancellationToken = default);
	Task ClearAsync(CancellationToken cancellationToken = default);
}
