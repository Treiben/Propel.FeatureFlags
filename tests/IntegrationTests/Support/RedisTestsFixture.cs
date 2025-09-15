using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Infrastructure.Redis;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace FeatureFlags.IntegrationTests.Support;

public class RedisTestsFixture : IAsyncLifetime
{
	private readonly RedisContainer _container;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	private ConnectionMultiplexer? _connectionMultiplexer = null!;
	private readonly ILogger<RedisFeatureFlagCache> _logger;

	public RedisTestsFixture()
	{
		_container = new RedisBuilder()
			.WithImage("redis:7-alpine")
			.WithPortBinding(6379, true)
			.Build();

		_logger = new Mock<ILogger<RedisFeatureFlagCache>>().Object;
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();
		var connectionString = _container.GetConnectionString();

		// Create connection multiplexer
		_connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(connectionString);

		// Initialize cache
		var cacheOptions = new CacheOptions
		{
			ExpiryInMinutes = TimeSpan.FromMinutes(1)
		};
		Cache = new RedisFeatureFlagCache(_connectionMultiplexer, cacheOptions, _logger);
	}

	public async Task DisposeAsync()
	{
		_connectionMultiplexer?.Dispose();
		await _container.DisposeAsync();
	}

	public IDatabase GetDatabase()
	{
		return _connectionMultiplexer.GetDatabase();
	}

	// Helper method to clear all feature flags between tests
	public async Task ClearAllFlags()
	{
		await Cache.ClearAsync();
	}
}
