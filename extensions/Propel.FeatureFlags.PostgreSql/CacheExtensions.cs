using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Infrastructure.Redis;
using StackExchange.Redis;

namespace Propel.FeatureFlags.PostgreSql;

internal static class CacheExtensions
{
	public static IServiceCollection AddRedisCache(this IServiceCollection services, string connectionString)
	{
		if (!string.IsNullOrEmpty(connectionString))
		{
			services.TryAddSingleton<IConnectionMultiplexer>(provider =>
			{
				var configurationOptions = ConfigurationOptions.Parse(connectionString); 
				configurationOptions.AbortOnConnectFail = false;
				configurationOptions.ConnectRetry = 3;
				configurationOptions.ConnectTimeout = 5000;

				return ConnectionMultiplexer.Connect(configurationOptions);
			});

			// Use the same connection for distributed cache
			services.TryAddSingleton<IDistributedCache>(provider =>
			{
				var multiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
				return new RedisCache(new RedisCacheOptions
				{
					ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer)
				});
			});

			services.AddSingleton<IFeatureFlagCache, RedisFeatureFlagCache>();
		}
		else
		{
			throw new InvalidOperationException("Redis connection string is required for Redis caching.");
		}
		return services;
	}
}
