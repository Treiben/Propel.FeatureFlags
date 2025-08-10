using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Cache
{
	public interface IFeatureFlagCache
	{
		Task<FeatureFlag?> GetAsync(string flagKey, CancellationToken cancellationToken = default);
		Task SetAsync(string flagKey, FeatureFlag flag, TimeSpan? expiry, CancellationToken cancellationToken = default);
		Task RemoveAsync(string flagKey, CancellationToken cancellationToken = default);
		Task ClearAsync(CancellationToken cancellationToken = default);
	}
}
