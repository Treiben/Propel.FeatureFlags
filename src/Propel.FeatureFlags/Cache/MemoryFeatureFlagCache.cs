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

		public Task<FeatureFlag?> GetAsync(string flagKey, CancellationToken cancellationToken = default)
		{
			_cache.TryGetValue(flagKey, out FeatureFlag? flag);
			return Task.FromResult(flag);
		}

		public Task SetAsync(string flagKey, FeatureFlag flag, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
		{
			_cache.Set(flagKey, flag, expiry ?? TimeSpan.FromMinutes(5));
			return Task.CompletedTask;
		}

		public Task RemoveAsync(string flagKey, CancellationToken cancellationToken = default)
		{
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
