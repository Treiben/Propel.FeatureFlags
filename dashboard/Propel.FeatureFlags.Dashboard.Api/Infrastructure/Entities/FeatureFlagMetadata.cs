namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.Entities;

public class FeatureFlagMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
	public string FlagKey { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "global";
    public string ApplicationVersion { get; set; } = "0.0.0.0";
    public bool IsPermanent { get; set; } = false;
    public DateTimeOffset ExpirationDate { get; set; }
    public string Tags { get; set; } = "{}";
    public FeatureFlag FeatureFlag { get; set; } = null!;
}