using Propel.FeatureFlags.Infrastructure.Cache;

namespace Propel.FeatureFlags.Infrastructure;

public class FeatureFlagConfigurationOptions
{
	public string? SqlConnectionString { get; set; }
	public string? AzureAppConfigConnectionString { get; set; }
	public string? RedisConnectionString { get; set; }
	public string DefaultTimeZone { get; set; } = "UTC";
	public CacheOptions CacheOptions { get; set; } = new CacheOptions();
}
