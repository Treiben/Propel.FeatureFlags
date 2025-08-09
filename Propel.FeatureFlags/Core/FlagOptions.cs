namespace Propel.FeatureFlags.Core;

public record FlagOptions
{
	public string? SqlConnectionString { get; set; }
	public string? AzureAppConfigConnectionString { get; set; }
	public string? RedisConnectionString { get; set; }
	public bool? UseCache { get; set; } = true;
	public string DefaultTimeZone { get; set; } = "UTC";
}
