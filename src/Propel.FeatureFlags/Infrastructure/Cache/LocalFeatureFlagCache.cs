using Microsoft.Extensions.Caching.Memory;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.Cache;

/// <summary>
/// Provides a local in-memory cache for feature flag evaluation options.
/// </summary>
/// <remarks>This implementation uses <see cref="IMemoryCache"/> to store and retrieve feature flag evaluation
/// options. The cache entries are configured with an absolute expiration and an optional sliding expiration,  based on
/// the provided <see cref="LocalCacheConfiguration"/>.</remarks>
/// <param name="cache"></param>
/// <param name="cacheConfiguration"></param>
public sealed class LocalFeatureFlagCache(IMemoryCache cache, LocalCacheConfiguration cacheConfiguration) : IFeatureFlagCache
{
	private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
	private readonly LocalCacheConfiguration _cacheConfiguration = cacheConfiguration ?? throw new ArgumentNullException(nameof(cacheConfiguration));

	/// <summary>
	/// Retrieves the <see cref="EvaluationOptions"/> associated with the specified cache key.
	/// </summary>
	/// <param name="cacheKey">The key used to locate the cached <see cref="EvaluationOptions"/>.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests. This parameter is optional and defaults to <see
	/// cref="CancellationToken.None"/>.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="EvaluationOptions"/>
	/// associated with the specified cache key, or <see langword="null"/> if no value is found.</returns>
	public Task<EvaluationOptions?> GetAsync(FlagCacheKey cacheKey, CancellationToken cancellationToken = default)
	{
		var flagKey = cacheKey.ComposeKey();
		_cache.TryGetValue(flagKey, out EvaluationOptions? flag);
		return Task.FromResult(flag);
	}

	/// <summary>
	/// Asynchronously sets a cache entry for the specified key with the given value and expiration options.
	/// </summary>
	/// <remarks>The cache entry will have both an absolute expiration and a sliding expiration, as configured by
	/// the cache settings. The absolute expiration ensures the entry is removed after a fixed duration, while the sliding
	/// expiration resets the expiration timer each time the entry is accessed.</remarks>
	/// <param name="cacheKey">The key used to identify the cache entry. Must be unique within the cache.</param>
	/// <param name="flag">The value to store in the cache for the specified key.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests. This parameter is optional and defaults to <see
	/// cref="CancellationToken.None"/>.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	public Task SetAsync(FlagCacheKey cacheKey, EvaluationOptions flag, CancellationToken cancellationToken = default)
	{
		var flagKey = cacheKey.ComposeKey();

		var memOptions = new MemoryCacheEntryOptions
		{
			// Ensure cache entry has an absolute expiration
			AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheConfiguration.CacheDurationInMinutes),
			// Sliding expiration is optional, but often useful
			SlidingExpiration = TimeSpan.FromMinutes(_cacheConfiguration.CacheDurationInMinutes),
			Priority = CacheItemPriority.Normal,
			Size = 1 // Assume each entry is approximately 1 unit in size
		};

		_cache.Set(flagKey, flag, memOptions);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Removes the specified cache entry asynchronously.
	/// </summary>
	/// <param name="cacheKey">The key identifying the cache entry to remove.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	public Task RemoveAsync(FlagCacheKey cacheKey, CancellationToken cancellationToken = default)
	{
		var flagKey = cacheKey.ComposeKey();
		_cache.Remove(flagKey);
		return Task.CompletedTask;
	}
}