namespace Propel.FeatureFlags.Infrastructure.Cache;

public class CacheConfiguration
{
	public bool? UseCache { get; set; } = true;

	public TimeSpan Expiry { get; set; } = TimeSpan.FromMinutes(5);
}
