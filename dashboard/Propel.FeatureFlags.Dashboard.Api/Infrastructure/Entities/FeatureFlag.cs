using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.Entities;

public class FeatureFlag
{
	public string Key { get; set; } = string.Empty;
	public string ApplicationName { get; set; } = "global";
	public string ApplicationVersion { get; set; } = "0.0.0.0";
	public int Scope { get; set; } = 0;

	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;

	public string EvaluationModes { get; set; } = "[]";

	// Scheduling
	public DateTimeOffset? ScheduledEnableDate { get; set; }
	public DateTimeOffset? ScheduledDisableDate { get; set; }

	// Time Windows
	public TimeOnly? WindowStartTime { get; set; }
	public TimeOnly? WindowEndTime { get; set; }
	public string? TimeZone { get; set; }
	public string WindowDays { get; set; } = "[]";

	// Targeting Rules
	public string TargetingRules { get; set; } = "[]";

	// User-level Controls
	public string EnabledUsers { get; set; } = "[]";
	public string DisabledUsers { get; set; } = "[]";
	public int UserPercentageEnabled { get; set; } = 100;

	// Tenant-level Controls
	public string EnabledTenants { get; set; } = "[]";
	public string DisabledTenants { get; set; } = "[]";
	public int TenantPercentageEnabled { get; set; } = 100;

	// Variations
	public string Variations { get; set; } = "{}";
	public string DefaultVariation { get; set; } = "off";

	// Metadata
	public FeatureFlagMetadata Metadata { get; set; } = new FeatureFlagMetadata();
	public List<FeatureFlagAudit> AuditTrail { get; set; } = [];
}
