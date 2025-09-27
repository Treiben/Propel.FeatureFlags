namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.Entities;

public class FeatureFlagAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FlagKey { get; set; } = string.Empty;
    public string? ApplicationName { get; set; } = "global";
    public string ApplicationVersion { get; set; } = "0.0.0.0";
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
	public FeatureFlag FeatureFlag { get; set; } = null!;
}