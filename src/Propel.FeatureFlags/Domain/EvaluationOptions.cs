using Knara.UtcStrict;

namespace Propel.FeatureFlags.Domain;

public class EvaluationOptions
{
	public string Key { get; } 
	public ModeSet ModeSet { get; }
	public UtcSchedule Schedule { get;}
	public UtcTimeWindow OperationalWindow { get; }
	public List<ITargetingRule> TargetingRules { get; }
	public AccessControl UserAccessControl { get; }
	public AccessControl TenantAccessControl { get; } 
	// Variations for A/B testing
	public Variations Variations { get; }

	public EvaluationOptions(string key,
		ModeSet? modeSet = null,
		UtcSchedule? schedule = null,
		UtcTimeWindow? operationalWindow = null,
		List<ITargetingRule>? targetingRules = null,
		AccessControl? userAccessControl = null,
		AccessControl? tenantAccessControl = null,
		Variations? variations = null)
	{
		Key = key ?? throw new ArgumentNullException(nameof(key));
		ModeSet = modeSet ?? EvaluationMode.Off;
		Schedule = schedule ?? UtcSchedule.Unscheduled;
		OperationalWindow = operationalWindow ?? UtcTimeWindow.AlwaysOpen;
		TargetingRules = targetingRules ?? [];
		UserAccessControl = userAccessControl ?? AccessControl.Unrestricted;
		TenantAccessControl = tenantAccessControl ?? AccessControl.Unrestricted;
		Variations = variations ?? Variations.OnOff;
	}
}
