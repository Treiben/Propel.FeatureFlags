using Microsoft.Extensions.Caching.Memory;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.Cache;

public sealed class InMemoryFlagCache(IMemoryCache cache, PropelOptions options) : IFeatureFlagCache
{
	private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
	private readonly CacheOptions _cacheConfiguration = options.Cache ?? throw new ArgumentNullException(nameof(CacheOptions));

	public Task<EvaluationOptions?> GetAsync(CacheKey cacheKey, CancellationToken cancellationToken = default)
	{
		var flagKey = cacheKey.ComposeKey();
		_cache.TryGetValue(flagKey, out EvaluationOptions? flag);
		return Task.FromResult(flag);
	}

	public Task SetAsync(CacheKey cacheKey, EvaluationOptions flag, CancellationToken cancellationToken = default)
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
		var pattern = $"{CacheKey.KEY_PREFIX}:";
		foreach (var key in ((MemoryCache)_cache).Keys)
		{
			if (((string)key).StartsWith(pattern))
				_cache.Remove(key);
		}
		return Task.CompletedTask;
	}
}
