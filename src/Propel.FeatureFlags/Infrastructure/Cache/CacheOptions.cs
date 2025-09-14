namespace Propel.FeatureFlags.Infrastructure.Cache;

public class CacheOptions
{
	public bool? UseCache { get; set; } = false;

	public TimeSpan ExpiryInMinutes { get; set; } = TimeSpan.FromMinutes(5);

	public TimeSpan SlidingExpiryInMinutes { get; set; } = TimeSpan.FromMinutes(30);
}
