using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Extensions;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Infrastructure.Redis.Extensions;
using Testcontainers.Redis;

namespace FeatureFlags.IntegrationTests.CacheTests;

public class RedisTestsFixture : IAsyncLifetime
{
	private readonly RedisContainer _container;
	public IServiceProvider Services { get; private set; } = null!;
	public IFeatureFlagCache Cache => Services.GetRequiredService<IFeatureFlagCache>();

	public RedisTestsFixture()
	{
		_container = new RedisBuilder()
			.WithImage("redis:7-alpine")
			.WithPortBinding(6379, true)
			.Build();
	}

	public async Task InitializeAsync()
	{
		var redisConnectionString = await StartContainer();

		var services = new ServiceCollection();

		services.AddLogging();

		services.ConfigureFeatureFlags(options =>
		{
			options.Cache = new CacheOptions
			{
				EnableDistributedCache = true,
				Connection = redisConnectionString,
				CacheDurationInMinutes = TimeSpan.FromMinutes(1)
			};
		});

		Services = services.BuildServiceProvider();
	}

	private async Task<string> StartContainer()
	{
		await _container.StartAsync();
		var connectionString = _container.GetConnectionString();
		return connectionString;
	}

	public async Task DisposeAsync()
	{
		await _container.DisposeAsync();
	}

	public async Task ClearAllFlags()
	{
		await Cache.ClearAsync();
	}
}

public class InMemoryTestsFixture : IAsyncLifetime
{
	public IServiceProvider Services { get; private set; } = null!;
	public IFeatureFlagCache Cache => Services.GetRequiredService<IFeatureFlagCache>();

	public async Task InitializeAsync()
	{
		var services = new ServiceCollection();

		services.AddLogging();

		services.ConfigureFeatureFlags(options =>
		{
			options.Cache = new CacheOptions
			{
				EnableInMemoryCache = true,
				CacheDurationInMinutes = TimeSpan.FromMinutes(1)
			};
		});

		Services = services.BuildServiceProvider();
	}

	public async Task DisposeAsync()
	{
		await Task.CompletedTask;
	}

	// Helper method to clear all feature flags between tests
	public async Task ClearAllFlags()
	{
		await Cache.ClearAsync();
	}

	// Helper method to check if a key exists in the underlying memory cache
	//public bool KeyExists(string key)
	//{
	//	return _memoryCache.TryGetValue(key, out _);
	//}

	//// Helper method to set a non-feature flag key directly in the memory cache
	//public void SetNonFeatureFlagKey(string key, object value)
	//{
	//	_memoryCache.Set(key, value);
	//}

	//// Helper method to check if a non-feature flag key exists
	//public bool NonFeatureFlagKeyExists(string key)
	//{
	//	return _memoryCache.TryGetValue(key, out _);
	//}
}

public static class ServiceCollectionExtensions
{
	public static IServiceCollection ConfigureFeatureFlags(this IServiceCollection services, Action<PropelOptions> configure)
	{
		var options = new PropelOptions();
		configure.Invoke(options);

		services.AddFeatureFlagServices(options);

		var cacheOptions = options.Cache;
		if (cacheOptions.EnableDistributedCache == true)
		{
			services.AddFeatureFlagRedisCache(cacheOptions.Connection);
		}
		else if (cacheOptions.EnableInMemoryCache == true)
		{
			services.AddFeatureFlagDefaultCache();
		}

		return services;
	}
}
