using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Infrastructure.Cache;
using StackExchange.Redis;

namespace Propel.FeatureFlags.Redis;

public static partial class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds Redis distributed caching to the specified <see cref="IServiceCollection"/>  using the provided connection
	/// string.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to which the Redis cache services will be added.</param>
	/// <param name="connectionString">The connection string used to connect to the Redis server. This value cannot be <see langword="null"/>  or empty.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance to allow for method chaining.</returns>
	public static IServiceCollection AddRedisCache(this IServiceCollection services,
		string connectionString)
	{
		return services.AddRedisCache(connectionString, null);
	}

	/// <summary>
	/// Adds Redis-based caching to the specified <see cref="IServiceCollection"/> with optional local memory caching and
	/// configurable Redis settings.
	/// </summary>
	/// <remarks>This method configures a Redis-based caching layer for distributed caching and optionally adds a
	/// local memory cache for improved performance. It registers the following services: <list type="bullet">
	/// <item><description><see cref="IConnectionMultiplexer"/> for managing the Redis connection.</description></item>
	/// <item><description><see cref="IDistributedCache"/> for distributed caching using Redis.</description></item>
	/// <item><description><see cref="IFeatureFlagCache"/> for feature flag caching.</description></item> </list> The local
	/// memory cache is configured with a size limit and compaction settings, and the Redis connection is established with
	/// production-ready settings. If the Redis connection fails, an exception is thrown.</remarks>
	/// <param name="services">The <see cref="IServiceCollection"/> to which the Redis cache services will be added.</param>
	/// <param name="connectionString">The connection string used to connect to the Redis server. This parameter cannot be null, empty, or whitespace.</param>
	/// <param name="configure">An optional delegate to configure additional settings for the Redis cache. If not provided, default settings will
	/// be used.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="connectionString"/> is null, empty, or consists only of whitespace.</exception>
	public static IServiceCollection AddRedisCache(this IServiceCollection services,
		string connectionString, Action<RedisCacheConfiguration>? configure)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new ArgumentException("Redis connection string is required for Redis caching.", nameof(connectionString));
		}

		// Use provided options or defaults
		var configuration = new RedisCacheConfiguration();
		configure?.Invoke(configuration);

		services.TryAddSingleton(configuration);

		// Add memory cache for local caching layer
		if (configuration.EnableInMemoryCache == true)
		{
			services.AddMemoryCache(memOptions =>
			{
				memOptions.SizeLimit = configuration.LocalCacheSizeLimit;
				memOptions.CompactionPercentage = 0.25; // Compact 25% when limit reached
			});
		}

		// Configure Redis connection with production-ready settings
		services.TryAddSingleton<IConnectionMultiplexer>(provider =>
		{
			var logger = provider.GetService<ILogger<IConnectionMultiplexer>>();

			try
			{
				var seConfigurationOptions = ConfigureRedisOptions(connectionString, configuration, logger);

				logger?.LogInformation("Connecting to Redis at {Endpoints}",
					string.Join(", ", seConfigurationOptions.EndPoints));

				var connection = ConnectionMultiplexer.Connect(seConfigurationOptions);

				// Log connection status
				if (connection.IsConnected)
				{
					logger?.LogInformation("Successfully connected to Redis");
				}
				else
				{
					logger?.LogWarning("Redis connection established but not yet connected");
				}

				return connection;
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, "Failed to connect to Redis. Feature flags will work without caching.");
				throw;
			}
		});

		// Register the same connection for IDistributedCache (if needed elsewhere)
		services.TryAddSingleton<IDistributedCache>(provider =>
		{
			var multiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
			return new RedisCache(new RedisCacheOptions
			{
				ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer),
				InstanceName = "propel-flags:" // Prefix for distributed cache keys
			});
		});

		// Register our feature flag cache implementation
		services.AddSingleton<IFeatureFlagCache, RedisFeatureFlagCache>();

		return services;
	}

	private static ConfigurationOptions ConfigureRedisOptions(string connectionString, RedisCacheConfiguration cacheConfiguration, ILogger? logger)
	{
		var configurationOptions = ConfigurationOptions.Parse(connectionString);

		configurationOptions.AbortOnConnectFail = false;
		configurationOptions.ConnectRetry = 3; 
		configurationOptions.ConnectTimeout = cacheConfiguration.RedisTimeoutMilliseconds; 
		configurationOptions.SyncTimeout = cacheConfiguration.RedisTimeoutMilliseconds; 
		configurationOptions.AsyncTimeout = cacheConfiguration.RedisTimeoutMilliseconds; 
		configurationOptions.KeepAlive = 60; 

		// cache retry policy
		//configurationOptions.ReconnectRetryPolicy = new ExponentialBackoffRetryPolicy(
		//	baseIntervalMilliseconds: 1000,    // Start with 1 second
		//	maxIntervalMilliseconds: 30000,    // Cap at 30 seconds
		//	maxRetryCount: cacheConfiguration.MaxReconnectAttempts,
		//	logger: logger);

		// Connection resilience
		configurationOptions.ConfigurationChannel = ""; // Disable config channel for better stability
		configurationOptions.TieBreaker = "";			// Disable tie breaker for simplicity

		// Performance optimizations for feature flags
		configurationOptions.AllowAdmin = false;		// No admin commands needed
		configurationOptions.ClientName = "PropelFeatureFlags"; // Identify our connections

		// SSL/TLS configuration (uncomment if using SSL)
		// configurationOptions.Ssl = true;
		// configurationOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;

		// Set reasonable defaults based on connection string format
		if (configurationOptions.EndPoints.Count == 0)
		{
			// Handle simple "host:port" format
			configurationOptions.EndPoints.Add(connectionString);
		}

		// Password handling for cloud providers
		if (connectionString.ToLower().Contains("rediss://"))
		{
			configurationOptions.Ssl = true;
		}

		logger?.LogDebug("Redis configuration: Endpoints={Endpoints}, SSL={Ssl}, ConnectTimeout={ConnectTimeout}ms",
			string.Join(", ", configurationOptions.EndPoints),
			configurationOptions.Ssl,
			configurationOptions.ConnectTimeout);

		return configurationOptions;
	}

	/// <summary>
	/// Extension method to validate Redis connection during startup
	/// </summary>
	public static async Task<bool> ValidateRedisConnectionAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
	{
		try
		{
			var multiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
			if (multiplexer == null)
			{
				return false;
			}

			var database = multiplexer.GetDatabase();
			var result = await database.PingAsync().ConfigureAwait(false);

			var logger = serviceProvider.GetService<ILogger<IConnectionMultiplexer>>();
			logger?.LogInformation("Redis ping successful. Latency: {Latency}", result);

			return true;
		}
		catch (Exception ex)
		{
			var logger = serviceProvider.GetService<ILogger<IConnectionMultiplexer>>();
			logger?.LogWarning(ex, "Redis connection validation failed");
			return false;
		}
	}
}