namespace Propel.FeatureFlags.Redis;

/// <summary>
/// Configuration options for feature flag caching
/// </summary>
public class RedisCacheConfiguration
{
	/// <summary>
	/// Gets or sets the Redis connection string.
	/// Examples:
	/// - "localhost:6379"
	/// - "localhost:6379,password=mypassword"
	/// - "cache.example.com:6380,ssl=true,password=xxx"
	/// </summary>
	public string? Connection { get; set; }

	/// <summary>
	/// Gets or sets whether to enable in-memory caching.
	/// Default: true
	/// Note: This is always true when Redis is enabled (two-level caching)
	/// </summary>
	public bool? EnableInMemoryCache { get; set; } = true;

	/// <summary>
	/// Gets or sets the cache duration in minutes for Redis entries.
	/// Default: 60 minutes
	/// Note: Actual expiration includes random jitter (±5 minutes) to prevent cache stampede
	/// </summary>
	public double CacheDurationInMinutes { get; set; } = 60;

	/// <summary>
	/// Gets or sets the local memory cache size limit (number of feature flags).
	/// Default: 1000 flags
	/// </summary>
	public int LocalCacheSizeLimit { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the local memory cache duration in seconds.
	/// Default: 10 seconds
	/// Note: Short duration reduces memory usage and improves consistency
	/// </summary>
	public int LocalCacheDurationSeconds { get; set; } = 10;

	/// <summary>
	/// Gets or sets the circuit breaker failure threshold.
	/// Default: 3 consecutive failures before opening
	/// </summary>
	public int CircuitBreakerThreshold { get; set; } = 3;

	/// <summary>
	/// Gets or sets the circuit breaker open duration in seconds.
	/// Default: 30 seconds
	/// </summary>
	public int CircuitBreakerDurationSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets Redis operation timeout in milliseconds.
	/// Default: 5000ms (5 seconds)
	/// </summary>
	public int RedisTimeoutMilliseconds { get; set; } = 5000;

	/// <summary>
	/// Gets or sets the maximum number of reconnection attempts.
	/// Default: 10 attempts
	/// </summary>
	public int MaxReconnectAttempts { get; set; } = 10;

	/// <summary>
	/// Validates the cache options and returns any validation errors.
	/// </summary>
	public IEnumerable<string> Validate()
	{
		if (string.IsNullOrWhiteSpace(Connection))
		{
			yield return "Redis connection string is required when distributed cache is enabled.";
		}

		if (CacheDurationInMinutes < 1)
		{
			yield return "Cache duration must be at least 1 minute.";
		}

		if (LocalCacheSizeLimit < 100)
		{
			yield return "Local cache size limit should be at least 100 for reasonable performance.";
		}

		if (LocalCacheDurationSeconds < 1)
		{
			yield return "Local cache duration must be at least 1 second.";
		}

		if (CircuitBreakerThreshold < 1)
		{
			yield return "Circuit breaker threshold must be at least 1.";
		}

		if (CircuitBreakerDurationSeconds < 5)
		{
			yield return "Circuit breaker duration should be at least 5 seconds.";
		}

		if (RedisTimeoutMilliseconds < 1000)
		{
			yield return "Redis timeout should be at least 1000ms (1 second).";
		}
	}

}