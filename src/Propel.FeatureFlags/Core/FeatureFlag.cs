namespace Propel.FeatureFlags.Core;

public class FeatureFlag
{
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public DateTime? ExpirationDate { get; set; }
	public FlagEvaluationModeSet EvaluationModeSet { get; set; } = FlagEvaluationModeSet.FlagIsDisabled;
	public FlagSchedule Schedule { get; set; } = FlagSchedule.Unscheduled;
	public FlagTimeWindow TimeWindow { get; set; } = FlagTimeWindow.AlwaysOpen;
	// Targeting
	public List<TargetingRule> TargetingRules { get; set; } = [];
	public FlagUserLevelControl Users { get; set; } = FlagUserLevelControl.Unrestricted;
	public FlagTenantLevelControl Tenants { get; set; } = FlagTenantLevelControl.Unrestricted;
	// Variations for A/B testing
	public FlagVariations Variations { get; set; } = FlagVariations.OnOff;
	// Metadata
	public Dictionary<string, string> Tags { get; set; } = [];
	public bool IsPermanent { get; set; } = false;
	public FlagAudit Audit { get; set; } = FlagAudit.New;
}
