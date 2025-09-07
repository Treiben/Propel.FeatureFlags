namespace Propel.FeatureFlags.Core;

public class FeatureFlag
{
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public FlagEvaluationModeSet EvaluationModeSet { get; set; } = FlagEvaluationModeSet.FlagIsDisabled;
	public FlagActivationSchedule Schedule { get; set; } = FlagActivationSchedule.Unscheduled;
	public FlagOperationalWindow OperationalWindow { get; set; } = FlagOperationalWindow.AlwaysOpen;
	public FlagLifecycle Lifecycle { get; set; } = FlagLifecycle.DefaultLifecycle;
	public FlagAuditRecord AuditRecord { get; set; } = FlagAuditRecord.NewFlag();
	// Targeting
	public List<TargetingRule> TargetingRules { get; set; } = [];
	public FlagUserAccessControl UserAccess { get; set; } = FlagUserAccessControl.Unrestricted;
	public FlagTenantAccessControl TenantAccess { get; set; } = FlagTenantAccessControl.Unrestricted;
	// Variations for A/B testing
	public FlagVariations Variations { get; set; } = FlagVariations.OnOff;
	// Metadata
	public Dictionary<string, string> Tags { get; set; } = [];
}
