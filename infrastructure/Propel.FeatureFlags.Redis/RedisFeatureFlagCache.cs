using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using StackExchange.Redis;
using System.Text.Json;

namespace Propel.FeatureFlags.Redis;

public class RedisFeatureFlagCache(IConnectionMultiplexer redis, ILogger<RedisFeatureFlagCache> logger) : IFeatureFlagCache
{
	private readonly IDatabase _database = redis.GetDatabase();
	private const string KEY_PREFIX = "ff:";

	public async Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default)
	{
		logger.LogDebug("Getting feature flag {Key} from cache", key);
		if (cancellationToken.IsCancellationRequested)
		{
			throw new OperationCanceledException(cancellationToken);
		}

		try
		{
			var value = await _database.StringGetAsync($"{KEY_PREFIX}{key}");
			if (!value.HasValue)
			{
				logger.LogDebug("Feature flag {Key} not found in cache", key);
				return null;
			}

			var flag = JsonSerializer.Deserialize<FeatureFlag>(value!, JsonDefaults.JsonOptions);
			logger.LogDebug("Feature flag {Key} retrieved from cache", flag?.Key);
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Failed to get feature flag {Key} from cache", key);
			return null;
		}
	}

	public async Task SetAsync(string key, FeatureFlag flag, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
	{
		logger.LogDebug("Setting feature flag {Key} in cache with expiration {Expiration}", key, expiration);
		if (cancellationToken.IsCancellationRequested)
		{
			throw new OperationCanceledException(cancellationToken);
		}

		try
		{
			var value = JsonSerializer.Serialize(flag, JsonDefaults.JsonOptions);
			logger.LogDebug("Serialized feature flag {Key} to JSON", key);
			if (await _database.StringSetAsync($"{KEY_PREFIX}{key}", value, expiration))
			{
				logger.LogDebug("Feature flag {Key} set in cache successfully", key);
			}
			else
			{
				logger.LogWarning("Failed to set feature flag {Key} in cache", key);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Failed to set feature flag {Key} in cache", key);
		}
	}

	public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
	{
		logger.LogDebug("Removing feature flag {Key} from cache", key);
		if (cancellationToken.IsCancellationRequested)
		{
			throw new OperationCanceledException(cancellationToken);
		}

		try
		{
			if (await _database.KeyDeleteAsync($"{KEY_PREFIX}{key}"))
				logger.LogDebug("Feature flag {Key} removed from cache successfully", key);
			else
				logger.LogWarning("Feature flag {Key} not found in cache", key);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Failed to remove feature flag {Key} from cache", key);
		}
	}

	public async Task ClearAsync(CancellationToken cancellationToken = default)
	{
		logger.LogDebug("Clearing all feature flags from cache");
		if (cancellationToken.IsCancellationRequested)
		{
			throw new OperationCanceledException(cancellationToken);
		}

		try
		{
			var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
			logger.LogDebug("Connected to Redis server {Server}", server.EndPoint);
			await foreach (var key in server.KeysAsync(pattern: $"{KEY_PREFIX}*"))
			{
				if (await _database.KeyDeleteAsync(key))
					logger.LogDebug("Removed feature flag {Key} from cache", key);
				else
					logger.LogWarning("Feature flag {Key} not found in cache", key);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Failed to clear feature flag cache");
		}
	}
}
