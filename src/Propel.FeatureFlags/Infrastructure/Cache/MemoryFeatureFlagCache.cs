using Microsoft.Extensions.Caching.Memory;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.Cache;

public sealed class MemoryFeatureFlagCache(MemoryCache cache, PropelOptions options) : IFeatureFlagCache
{
	private readonly MemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
	private readonly CacheOptions _cacheConfiguration = options.Cache ?? throw new ArgumentNullException(nameof(CacheOptions));

	public Task<FlagEvaluationConfiguration?> GetAsync(CacheKey cacheKey, CancellationToken cancellationToken = default)
	{
		var flagKey = cacheKey.ComposeKey();
		_cache.TryGetValue(flagKey, out FlagEvaluationConfiguration? flag);
		return Task.FromResult(flag);
	}

	public Task SetAsync(CacheKey cacheKey, FlagEvaluationConfiguration flag, CancellationToken cancellationToken = default)
	{
		var flagKey = cacheKey.ComposeKey();
		var memOptions = new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = _cacheConfiguration.CacheDurationInMinutes,
			SlidingExpiration = _cacheConfiguration.CacheDurationInMinutes,
			Priority = CacheItemPriority.Normal,
		};
		_cache.Set(flagKey, flag, memOptions);
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
