#if NET462_OR_GREATER
using System.Runtime.Caching;
#endif
using System.Collections.Concurrent;
using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Cache
{
	public sealed class SystemMemoryCache : IFeatureFlagCache, IDisposable
	{
#if NET462_OR_GREATER
		private readonly MemoryCache _frameworkCache;
		private readonly string _cacheNamePrefix;
#else
		private readonly ConcurrentDictionary<string, CacheItem> _cache;
		private readonly Timer _cleanupTimer;
#endif
		private bool _disposed;

		public SystemMemoryCache()
		{
#if NET462_OR_GREATER
			_cacheNamePrefix = $"FeatureFlag_{Guid.NewGuid():N}_";
			_frameworkCache = MemoryCache.Default;
#else
			_cache = new ConcurrentDictionary<string, CacheItem>();
			_cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
#endif
		}

		public Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(SystemMemoryCache));

			if (string.IsNullOrEmpty(key))
				return Task.FromResult<FeatureFlag?>(null);

#if NET462_OR_GREATER
			var cacheKey = _cacheNamePrefix + key;
			var flag = _frameworkCache.Get(cacheKey) as FeatureFlag;
			return Task.FromResult(flag);
#else
			if (_cache.TryGetValue(key, out var cacheItem))
			{
				if (cacheItem.ExpiresAt.HasValue && DateTime.UtcNow > cacheItem.ExpiresAt.Value)
				{
					_cache.TryRemove(key, out _);
					return Task.FromResult<FeatureFlag?>(null);
				}
				return Task.FromResult<FeatureFlag?>(cacheItem.Value);
			}
			return Task.FromResult<FeatureFlag?>(null);
#endif
		}

		public Task SetAsync(string key, FeatureFlag flag, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(SystemMemoryCache));

			if (string.IsNullOrEmpty(key) || flag == null)
				return Task.CompletedTask;

#if NET462_OR_GREATER
			var cacheKey = _cacheNamePrefix + key;
			var policy = new CacheItemPolicy();
			if (expiration.HasValue)
			{
				policy.AbsoluteExpiration = DateTimeOffset.UtcNow.Add(expiration.Value);
			}
			else
			{
				policy.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(5);
			}
			_frameworkCache.Set(cacheKey, flag, policy);
#else
			var expiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : (DateTime?)null;
			var cacheItem = new CacheItem(flag, expiresAt);
			_cache.AddOrUpdate(key, cacheItem, (k, v) => cacheItem);
#endif

			return Task.CompletedTask;
		}

		public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(SystemMemoryCache));

			if (!string.IsNullOrEmpty(key))
			{
#if NET462_OR_GREATER
				var cacheKey = _cacheNamePrefix + key;
				_frameworkCache.Remove(cacheKey);
#else
				_cache.TryRemove(key, out _);
#endif
			}

			return Task.CompletedTask;
		}

		public Task ClearAsync(CancellationToken cancellationToken = default)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(SystemMemoryCache));

#if NET462_OR_GREATER
			// Remove all items with our prefix
			var keysToRemove = _frameworkCache
				.Where(kvp => kvp.Key.StartsWith(_cacheNamePrefix))
				.Select(kvp => kvp.Key)
				.ToList();
		
			foreach (var key in keysToRemove)
			{
				_frameworkCache.Remove(key);
			}
#else
			_cache.Clear();
#endif

			return Task.CompletedTask;
		}

#if !NET462_OR_GREATER
		private void CleanupExpiredItems(object? state)
		{
			if (_disposed) return;

			var now = DateTime.UtcNow;
			var keysToRemove = _cache
				.Where(kvp => kvp.Value.ExpiresAt.HasValue && now > kvp.Value.ExpiresAt.Value)
				.Select(kvp => kvp.Key)
				.ToList();

			foreach (var key in keysToRemove)
			{
				_cache.TryRemove(key, out _);
			}
		}

		private sealed class CacheItem
		{
			public FeatureFlag Value { get; }
			public DateTime? ExpiresAt { get; }

			public CacheItem(FeatureFlag value, DateTime? expiresAt)
			{
				Value = value;
				ExpiresAt = expiresAt;
			}
		}
#endif

		public void Dispose()
		{
			if (!_disposed)
			{
#if !NET462_OR_GREATER
				_cleanupTimer?.Dispose();
				_cache.Clear();
#endif
				_disposed = true;
			}
		}
	}
}