namespace Propel.FeatureFlags.Infrastructure;

public class PropelConfiguration
{
	public bool RegisterFlagsWithContainer { get; set; } = false;
	public bool AutoDeployFlags { get; set; } = false;
	public bool EnableFlagFactory { get; set; } = true;
	public string DefaultTimeZone { get; set; } = "UTC";
	public string SqlConnection { get; set; } = "";
	public CacheOptions Cache { get; set; } = new CacheOptions();
	public AOPOptions Interception { get; set; } = new AOPOptions();
}

public class CacheOptions
{
	public string Connection { get; set; } = "";
	/// <summary>
	/// Enable or disable in-memory caching of feature flag evaluations
	/// Default: true (recommended for performance)
	/// </summary>
	public bool EnableInMemoryCache { get; set; } = false;
	/// <summary>
	/// Duration to cache feature flag evaluations in memory
	/// Default: 5 minutes
	/// </summary>
	public bool EnableDistributedCache { get; set; } = false;
	/// <summary>
	/// Duration to cache feature flag evaluations in distributed cache
	/// Default: 30 minutes
	/// </summary>
	public TimeSpan CacheDurationInMinutes { get; set; } = TimeSpan.FromMinutes(30);
	/// <summary>
	/// Remove flag from cache if not accessed within this sliding duration
	/// Default: 5 minutes
	/// </summary>
	public TimeSpan SlidingDurationInMinutes { get; set; } = TimeSpan.FromMinutes(5);
}

public enum DatabaseProvider
{
	PostgreSQL,
	SqlServer
}

public class AOPOptions
{
	public bool EnableHttpIntercepter { get; set; }
	public bool EnableIntercepter { get; set; }
}
