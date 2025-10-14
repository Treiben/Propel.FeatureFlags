namespace Propel.FeatureFlags.Infrastructure.Cache;

/// <summary>
/// Represents the configuration settings for a local cache, including options for cache duration, size limits, and
/// expiration policies.
/// </summary>
/// <remarks>This class provides properties to configure the behavior of a local cache, such as enabling or
/// disabling the cache, setting cache durations, and defining size limits. It also includes methods for validating the
/// configuration and creating predefined configurations for production and development environments.</remarks>
public class LocalCacheConfiguration
{
	/// <summary>
	/// Gets a value indicating whether the local cache is enabled.
	/// </summary>
	public bool LocalCacheEnabled { get; set; } = false;

	/// <summary>
	/// Gets or sets the cache duration in minutes for Redis entries.
	/// Default: 60 minutes
	/// Note: Actual expiration includes random jitter (±5 minutes) to prevent cache stampede
	/// </summary>
	public double CacheDurationInMinutes { get; set; } = 60;

	/// <summary>
	/// Gets or sets the sliding expiration duration in minutes.
	/// Note: Not currently used. The implementation uses automatic sliding expiration
	/// where frequently accessed flags get their expiration refreshed on read.
	/// </summary>
	public double? SlidingDurationInMinutes { get; set; } = 5;

	/// <summary>
	/// Gets or sets the local memory cache size limit (number of feature flags).
	/// Default: 1000 flags
	/// </summary>
	public int CacheSizeLimit { get; set; } = 1000;


	/// <summary>
	/// Validates the cache options and returns any validation errors.
	/// </summary>
	public IEnumerable<string> Validate()
	{
		if (CacheDurationInMinutes < 1)
		{
			yield return "Cache duration must be at least 1 minute.";
		}

		if (CacheSizeLimit < 100)
		{
			yield return "Local cache size limit should be at least 100 for reasonable performance.";
		}
	}
}
