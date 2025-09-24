namespace Propel.FeatureFlags.Infrastructure;

public class PropelOptions
{
	public bool RegisterFlagsWithContainer { get; set; } = false;
	public bool InsertFlagsInDatabase { get; set; } = false;
	public string DefaultTimeZone { get; set; } = "UTC";
	public DatabaseOptions Database { get; set; } = new DatabaseOptions();
	public CacheOptions Cache { get; set; } = new CacheOptions();
	public AOPOptions AttributeIntercepting { get; set; } = new AOPOptions();
}

public class CacheOptions
{
	public string Connection { get; set; } = string.Empty;
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

public class DatabaseOptions
{
	public string ConnectionString { get; set; } = string.Empty;

	public DatabaseProvider? Provider { get; set; }
}

public enum DatabaseProvider
{
	PostgreSQL,
	SqlServer
}

public class AOPOptions
{
	public bool EnableHttpIntercepter { get; set; } = false;
	public bool EnableIntercepter { get; set; } = false;
}
