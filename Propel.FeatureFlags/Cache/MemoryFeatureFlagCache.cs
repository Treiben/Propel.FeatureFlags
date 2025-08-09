using Microsoft.Extensions.Caching.Memory;
using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Cache;

public sealed class MemoryFeatureFlagCache(MemoryCache cache) : IFeatureFlagCache
{
	private readonly MemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

	public Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default)
	{
		_cache.TryGetValue(key, out FeatureFlag? flag);
		return Task.FromResult(flag);
	}

	public Task SetAsync(string key, FeatureFlag flag, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
	{
		_cache.Set(key, flag, expiration ?? TimeSpan.FromMinutes(5));
		return Task.CompletedTask;
	}

	public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
	{
		_cache.Remove(key);
		return Task.CompletedTask;
	}

	public Task ClearAsync(CancellationToken cancellationToken = default)
	{
		_cache.Clear();
		return Task.CompletedTask;
	}
}
