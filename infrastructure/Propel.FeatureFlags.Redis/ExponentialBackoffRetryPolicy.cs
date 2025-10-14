using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Propel.FeatureFlags.Redis;

// Exponential backoff retry policy with jitter for connection resilience
internal class ExponentialBackoffRetryPolicy : IReconnectRetryPolicy
{
	private readonly int _baseIntervalMilliseconds;
	private readonly int _maxIntervalMilliseconds;
	private readonly int _maxRetryCount;
	private readonly ILogger? _logger;

	public ExponentialBackoffRetryPolicy(
		int baseIntervalMilliseconds = 1000,
		int maxIntervalMilliseconds = 30000,
		int maxRetryCount = 10,
		ILogger? logger = null)
	{
		_baseIntervalMilliseconds = baseIntervalMilliseconds;
		_maxIntervalMilliseconds = maxIntervalMilliseconds;
		_maxRetryCount = maxRetryCount;
		_logger = logger;
	}

	public bool ShouldRetry(long currentRetryCount, int timeElapsedMillisecondsSinceLastRetry)
	{
		// Stop retrying after max attempts
		if (currentRetryCount >= _maxRetryCount)
		{
			_logger?.LogWarning("Redis reconnection abandoned after {RetryCount} attempts", currentRetryCount);
			return false;
		}

		// For first attempt, always retry
		if (currentRetryCount == 0)
		{
			_logger?.LogDebug("Initial Redis reconnection attempt");
			return true;
		}

		// Calculate minimum time that should have elapsed based on previous interval
		var expectedInterval = RetryIntervalMilliseconds(currentRetryCount - 1);
		var minElapsedTime = expectedInterval * 0.8; // Allow 20% tolerance

		// Only retry if enough time has passed since last attempt
		if (timeElapsedMillisecondsSinceLastRetry >= minElapsedTime)
		{
			_logger?.LogDebug("Redis reconnection attempt {RetryCount} after {ElapsedMs}ms",
				currentRetryCount + 1, timeElapsedMillisecondsSinceLastRetry);
			return true;
		}

		// Too soon since last retry - prevent tight retry loops
		_logger?.LogTrace("Skipping Redis retry - only {ElapsedMs}ms elapsed, need {MinMs}ms",
			timeElapsedMillisecondsSinceLastRetry, minElapsedTime);
		return false;
	}

	public int RetryIntervalMilliseconds(long currentRetryCount)
	{
		// Exponential backoff: 1s, 2s, 4s, 8s, 16s, 30s, 30s...
		var exponentialInterval = _baseIntervalMilliseconds * Math.Pow(2, Math.Min(currentRetryCount, 5));
		var interval = (int)Math.Min(exponentialInterval, _maxIntervalMilliseconds);

		// Add jitter (±20%) to prevent thundering herd
		var jitterRange = interval / 5;
		var jitter = RandomJitter.Next(-jitterRange, jitterRange);

		var finalInterval = Math.Max(_baseIntervalMilliseconds, interval + jitter);

		_logger?.LogDebug("Next Redis reconnection in {IntervalMs}ms (attempt {RetryCount})",
			finalInterval, currentRetryCount + 1);

		return finalInterval;
	}
}
