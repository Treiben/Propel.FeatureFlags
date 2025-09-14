using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Infrastructure.Cache;
using StackExchange.Redis;
using System.Net;
using System.Text.Json;

namespace Propel.FeatureFlags.Infrastructure.Redis;

public class RedisFeatureFlagCache(
	IConnectionMultiplexer redis, 
	CacheConfiguration cacheConfiguration, 
	ILogger<RedisFeatureFlagCache> logger) : IFeatureFlagCache
{
	private readonly IDatabase _database = redis.GetDatabase();
	private readonly CacheConfiguration _cacheConfiguration = cacheConfiguration ?? throw new ArgumentNullException(nameof(cacheConfiguration));


	public async Task<FeatureFlag?> GetAsync(CacheKey cacheKey, CancellationToken cancellationToken = default)
	{
		var key = cacheKey.ComposeKey();
		logger.LogDebug("Getting feature flag {Key} from cache", key);
		if (cancellationToken.IsCancellationRequested)
		{
			throw new OperationCanceledException(cancellationToken);
		}

		try
		{
			var value = await _database.StringGetAsync(key);
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

	public async Task SetAsync(CacheKey cacheKey, FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		var key = cacheKey.ComposeKey();
		logger.LogDebug("Setting feature flag {Key} in cache with expiration {Expiration}", key, _cacheConfiguration.Expiry);
		if (cancellationToken.IsCancellationRequested)
		{
			throw new OperationCanceledException(cancellationToken);
		}

		try
		{
			var value = JsonSerializer.Serialize(flag, JsonDefaults.JsonOptions);
			logger.LogDebug("Serialized feature flag {Key} to JSON", key);
			if (await _database.StringSetAsync(key, value, _cacheConfiguration.Expiry))
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

	public async Task RemoveAsync(CacheKey cacheKey, CancellationToken cancellationToken = default)
	{
		var key = cacheKey.ComposeKey();
		logger.LogDebug("Removing feature flag {Key} from cache", key);
		if (cancellationToken.IsCancellationRequested)
		{
			throw new OperationCanceledException(cancellationToken);
		}

		try
		{
			if (await _database.KeyDeleteAsync(key))
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

		try
		{
			var endpoints = _database.Multiplexer.GetEndPoints();
			var tasks = new List<Task>();

			// Handle multiple Redis nodes (cluster/sentinel scenarios)
			foreach (var endpoint in endpoints)
			{
				tasks.Add(ClearFromServer(endpoint, cancellationToken));
			}

			await Task.WhenAll(tasks);
			logger.LogDebug("Successfully cleared all feature flags from cache");
		}
		catch (OperationCanceledException)
		{
			logger.LogInformation("Clear cache operation was cancelled");
			throw;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to clear feature flag cache");
			throw;
		}
	}

	private async Task ClearFromServer(EndPoint endpoint, CancellationToken cancellationToken)
	{
		var server = _database.Multiplexer.GetServer(endpoint);

		const int batchSize = 50; // Smaller batches for better performance
		var keysToDelete = new List<RedisKey>();

		// KeysAsync with pageSize uses SCAN internally - non-blocking
		await foreach (var key in server.KeysAsync(
			pattern: CacheKey.Pattern,
			pageSize: batchSize))
		{
			cancellationToken.ThrowIfCancellationRequested();

			keysToDelete.Add(key);

			// Delete in batches to avoid large single operations
			if (keysToDelete.Count >= batchSize)
			{
				await DeleteBatch(keysToDelete, cancellationToken);
				keysToDelete.Clear();
			}
		}

		// Delete any remaining keys
		if (keysToDelete.Count > 0)
		{
			await DeleteBatch(keysToDelete, cancellationToken);
		}
	}

	private async Task DeleteBatch(List<RedisKey> keys, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var deletedCount = await _database.KeyDeleteAsync(keys.ToArray());
			logger.LogDebug("Deleted {Count} of {Total} feature flag keys from cache",
				deletedCount, keys.Count);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to delete batch of {Count} keys", keys.Count);
			// Continue processing other batches rather than failing completely
		}
	}
}
