using Microsoft.Extensions.Caching.Memory;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace FeatureFlags.IntegrationTests.CacheTests;

public class InMemoryTestsFixture : IAsyncLifetime
{
	public InMemoryFlagCache Cache { get; private set; } = null!;
	private IMemoryCache _memoryCache = null!;

	public async Task InitializeAsync()
	{
		_memoryCache = new MemoryCache(new MemoryCacheOptions());

		// Initialize cache with the same configuration as Redis tests
		var options = new PropelOptions
		{
			Cache = new CacheOptions
			{
				CacheDurationInMinutes = TimeSpan.FromMinutes(1)
			}
		};
		Cache = new InMemoryFlagCache(_memoryCache, options);

		await Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		_memoryCache?.Dispose();
		await Task.CompletedTask;
	}

	// Helper method to clear all feature flags between tests
	public async Task ClearAllFlags()
	{
		await Cache.ClearAsync();
	}

	// Helper method to check if a key exists in the underlying memory cache
	public bool KeyExists(string key)
	{
		return _memoryCache.TryGetValue(key, out _);
	}

	// Helper method to set a non-feature flag key directly in the memory cache
	public void SetNonFeatureFlagKey(string key, object value)
	{
		_memoryCache.Set(key, value);
	}

	// Helper method to check if a non-feature flag key exists
	public bool NonFeatureFlagKeyExists(string key)
	{
		return _memoryCache.TryGetValue(key, out _);
	}
}