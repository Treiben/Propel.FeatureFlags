using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Utilities;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;

namespace Propel.FeatureFlags.Redis;


internal sealed class RedisFeatureFlagCache : IFeatureFlagCache, IDisposable
{
	private readonly IDatabase _database;
	private readonly IConnectionMultiplexer _redis;
	private readonly RedisCacheConfiguration _cacheConfiguration;
	private readonly IMemoryCache? _localCache;
	private readonly ILogger<RedisFeatureFlagCache>? _logger;
	private readonly TimeSpan _localCacheExpiration;

	// Circuit breaker for Redis failures
	private DateTime _circuitBreakerOpenTime = DateTime.MinValue;
	private readonly TimeSpan _circuitBreakerDuration;
	private int _consecutiveFailures = 0;
	private readonly int _circuitBreakerThreshold;

	public RedisFeatureFlagCache(
		IConnectionMultiplexer redis,
		RedisCacheConfiguration cacheConfiguration,
		IMemoryCache? memoryCache,
		ILogger<RedisFeatureFlagCache>? logger)
	{
		_redis = redis;
		_database = redis.GetDatabase();
		_cacheConfiguration = cacheConfiguration;
		_localCache = memoryCache;
		_logger = logger;

		// Configure from options
		_localCacheExpiration = TimeSpan.FromSeconds(_cacheConfiguration.LocalCacheDurationSeconds);
		_circuitBreakerThreshold = _cacheConfiguration.CircuitBreakerThreshold;
		_circuitBreakerDuration = TimeSpan.FromSeconds(_cacheConfiguration.CircuitBreakerDurationSeconds);

		// Subscribe to connection events for monitoring
		_redis.ConnectionFailed += OnConnectionFailed;
		_redis.ConnectionRestored += OnConnectionRestored;
	}

	public async Task<EvaluationOptions?> GetAsync(CacheKey cacheKey, CancellationToken cancellationToken = default)
	{
		var key = cacheKey.ComposeKey();

		// Check local cache first
		if (_localCache?.TryGetValue(key, out EvaluationOptions? cachedValue) ?? false)
		{
			_logger?.LogDebug("Feature flag {Key} found in local cache", cacheKey.Key);
			return cachedValue;
		}

		// Check circuit breaker
		if (IsCircuitBreakerOpen())
		{
			_logger?.LogWarning("Circuit breaker is open, skipping Redis for flag {Key}", cacheKey.Key);
			return null;
		}

		try
		{
			var sw = Stopwatch.StartNew();
			var value = await _database.StringGetAsync(key).ConfigureAwait(false);
			sw.Stop();

			if (!value.HasValue)
			{
				_logger?.LogDebug("Cache miss for flag {Key} (took {ElapsedMs}ms)", cacheKey.Key, sw.ElapsedMilliseconds);
				ResetCircuitBreaker();
				return null;
			}

			var flag = JsonSerializer.Deserialize<EvaluationOptions>(value!, JsonDefaults.JsonOptions);

			// Store in local cache with short expiration
			_localCache?.Set(key: key, value: flag, options: new MemoryCacheEntryOptions()
			{
				Size = 1, // Assume each entry is approximately 1 unit in size
				AbsoluteExpirationRelativeToNow = _localCacheExpiration
			});

			// Update expiration in Redis (sliding expiration for frequently accessed flags)
			await RefreshExpirationAsync(key).ConfigureAwait(false);

			_logger?.LogDebug("Cache hit for flag {Key} (took {ElapsedMs}ms)", cacheKey.Key, sw.ElapsedMilliseconds);
			ResetCircuitBreaker();

			return flag;
		}
		catch (RedisTimeoutException ex)
		{
			RecordFailure();
			_logger?.LogWarning(ex, "Redis timeout getting flag {Key}", cacheKey.Key);
			return null;
		}
		catch (RedisConnectionException ex)
		{
			RecordFailure();
			_logger?.LogWarning(ex, "Redis connection error getting flag {Key}", cacheKey.Key);
			return null;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			RecordFailure();
			_logger?.LogError(ex, "Unexpected error getting flag {Key} from cache", cacheKey.Key);
			return null;
		}
	}

	public async Task SetAsync(CacheKey cacheKey, EvaluationOptions flag, CancellationToken cancellationToken = default)
	{
		var key = cacheKey.ComposeKey();

		// Always update local cache immediately
		_localCache?.Set(key: key, value: flag, options: new MemoryCacheEntryOptions()
		{
			Size = 1, // Assume each entry is approximately 1 unit in size
			AbsoluteExpirationRelativeToNow = _localCacheExpiration
		});

		// Check circuit breaker
		if (IsCircuitBreakerOpen())
		{
			_logger?.LogWarning("Circuit breaker is open, skipping Redis write for flag {Key}", cacheKey.Key);
			return;
		}

		try
		{
			var value = JsonSerializer.Serialize(flag, JsonDefaults.JsonOptions);
			var expiry = GetExpiration();

			var sw = Stopwatch.StartNew();
			var result = await _database.StringSetAsync(key, value, expiry).ConfigureAwait(false);
			sw.Stop();

			if (result)
			{
				_logger?.LogDebug("Flag {Key} cached successfully (took {ElapsedMs}ms)", cacheKey.Key, sw.ElapsedMilliseconds);
				ResetCircuitBreaker();
			}
			else
			{
				_logger?.LogWarning("Failed to cache flag {Key}", cacheKey.Key);
			}
		}
		catch (RedisException ex)
		{
			RecordFailure();
			_logger?.LogWarning(ex, "Redis error caching flag {Key}", cacheKey.Key);
			// Don't throw - local cache is already updated
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			RecordFailure();
			_logger?.LogError(ex, "Unexpected error caching flag {Key}", cacheKey.Key);
			// Don't throw - local cache is already updated
		}
	}

	public async Task RemoveAsync(CacheKey cacheKey, CancellationToken cancellationToken = default)
	{
		var key = cacheKey.ComposeKey();

		// Always remove from local cache immediately
		_localCache?.Remove(key);

		if (IsCircuitBreakerOpen())
		{
			_logger?.LogWarning("Circuit breaker is open, skipping Redis delete for flag {Key}", cacheKey.Key);
			return;
		}

		try
		{
			var result = await _database.KeyDeleteAsync(key).ConfigureAwait(false);

			if (result)
			{
				_logger?.LogDebug("Flag {Key} removed from cache", cacheKey.Key);
				ResetCircuitBreaker();
			}
			else
			{
				_logger?.LogDebug("Flag {Key} was not in cache", cacheKey.Key);
			}
		}
		catch (RedisException ex)
		{
			RecordFailure();
			_logger?.LogWarning(ex, "Redis error removing flag {Key}", cacheKey.Key);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			RecordFailure();
			_logger?.LogError(ex, "Unexpected error removing flag {Key}", cacheKey.Key);
		}
	}

	private async Task RefreshExpirationAsync(string key)
	{
		try
		{
			// Fire and forget - don't wait for this operation
			await _database.KeyExpireAsync(key, GetExpiration(), flags: CommandFlags.FireAndForget)
				.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger?.LogDebug(ex, "Failed to refresh expiration for key {Key}", key);
			// Don't throw - this is a non-critical operation
		}
	}

	private TimeSpan GetExpiration()
	{
		// Add some jitter to prevent cache stampede
		var jitterMinutes = RandomJitter.Next(-5, 5);
		var totalMinutes = _cacheConfiguration.CacheDurationInMinutes + jitterMinutes;
		return TimeSpan.FromMinutes(Math.Max(1, totalMinutes));
	}

	// Circuit breaker implementation
	private bool IsCircuitBreakerOpen()
	{
		var failures = Interlocked.CompareExchange(ref _consecutiveFailures, 0, 0); // Thread-safe read

		if (failures >= _circuitBreakerThreshold)
		{
			if (DateTime.UtcNow - _circuitBreakerOpenTime < _circuitBreakerDuration)
			{
				return true;
			}

			// Try to close the circuit
			_logger?.LogInformation("Attempting to close circuit breaker");
			Interlocked.Exchange(ref _consecutiveFailures, 0);
		}

		return false;
	}

	private void RecordFailure()
	{
		var failures = Interlocked.Increment(ref _consecutiveFailures);

		if (failures == _circuitBreakerThreshold)
		{
			_circuitBreakerOpenTime = DateTime.UtcNow;
			_logger?.LogWarning("Circuit breaker opened after {Threshold} consecutive failures", _circuitBreakerThreshold);
		}
	}

	private void ResetCircuitBreaker()
	{
		var previousFailures = Interlocked.Exchange(ref _consecutiveFailures, 0);

		if (previousFailures > 0)
		{
			_logger?.LogInformation("Circuit breaker reset");
		}
	}

	// Event handlers for monitoring
	private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
	{
		_logger?.LogWarning("Redis connection failed: {FailureType} for endpoint {EndPoint}",
			e.FailureType, e.EndPoint);
		RecordFailure();
	}

	private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
	{
		_logger?.LogInformation("Redis connection restored for endpoint {EndPoint}", e.EndPoint);
		ResetCircuitBreaker();
	}

	public void Dispose()
	{
		_redis.ConnectionFailed -= OnConnectionFailed;
		_redis.ConnectionRestored -= OnConnectionRestored;
	}
}