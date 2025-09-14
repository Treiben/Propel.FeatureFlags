using Microsoft.Extensions.Caching.Memory;
using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Cache
{
	public sealed class MemoryFeatureFlagCache : IFeatureFlagCache
	{
		private readonly MemoryCache _cache;
		public MemoryFeatureFlagCache(MemoryCache cache)
		{
			_cache = cache ?? throw new ArgumentNullException(nameof(cache));
		}

		public Task<FeatureFlag?> GetAsync(CacheKey cacheKey, CancellationToken cancellationToken = default)
		{
			var flagKey = cacheKey.ComposeKey();
			_cache.TryGetValue(flagKey, out FeatureFlag? flag);
			return Task.FromResult(flag);
		}

		public Task SetAsync(CacheKey cacheKey, FeatureFlag flag, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
		{
			var flagKey = cacheKey.ComposeKey();
			_cache.Set(flagKey, flag, expiry ?? TimeSpan.FromMinutes(5));
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
}
