using Microsoft.Extensions.Caching.Memory;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.Cache;

public sealed class MemoryFeatureFlagCache(MemoryCache cache, CacheConfiguration cacheConfiguration) : IFeatureFlagCache
{
	private readonly MemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
	private readonly CacheConfiguration _cacheConfiguration = cacheConfiguration ?? throw new ArgumentNullException(nameof(cacheConfiguration));

	public Task<FeatureFlag?> GetAsync(CacheKey cacheKey, CancellationToken cancellationToken = default)
	{
		var flagKey = cacheKey.ComposeKey();
		_cache.TryGetValue(flagKey, out FeatureFlag? flag);
		return Task.FromResult(flag);
	}

	public Task SetAsync(CacheKey cacheKey, FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		var flagKey = cacheKey.ComposeKey();
		_cache.Set(flagKey, flag, _cacheConfiguration.Expiry);
		return Task.CompletedTask;
	}

	public Task RemoveAsync(CacheKey cacheKey, CancellationToken cancellationToken = default)
	{
		var flagKey = cacheKey.ComposeKey();
		_cache.Remove(flagKey);
		return Task.CompletedTask;
	}

	public Task ClearAsync(CancellationToken cancellationToken = default)
	{
		_cache.Clear();
		return Task.CompletedTask;
	}
}
