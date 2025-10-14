using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Redis;
using Testcontainers.Redis;
using Propel.FeatureFlags.DependencyInjection.Extensions;

namespace FeatureFlags.IntegrationTests.Redis.CacheTests;

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
		services.AddRedisCache(redisConnectionString);
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
}

public class InMemoryTestsFixture : IAsyncLifetime
{
	public IServiceProvider Services { get; private set; } = null!;
	public IFeatureFlagCache Cache => Services.GetRequiredService<IFeatureFlagCache>();

	public async Task InitializeAsync()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddLocalCache(new LocalCacheConfiguration
		{
			LocalCacheEnabled = true,
			CacheSizeLimit = 1024 // 1 MB
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
	}
}
