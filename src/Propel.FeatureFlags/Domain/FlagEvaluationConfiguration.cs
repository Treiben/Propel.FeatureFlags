using Knara.UtcStrict;

namespace Propel.FeatureFlags.Domain;

public class FlagEvaluationConfiguration
{
	public FlagIdentifier Identifier { get; } 
	public EvaluationModes ActiveEvaluationModes { get; }
	public UtcSchedule Schedule { get;}
	public UtcTimeWindow OperationalWindow { get; }
	public List<ITargetingRule> TargetingRules { get; }
	public AccessControl UserAccessControl { get; }
	public AccessControl TenantAccessControl { get; } 
	// Variations for A/B testing
	public Variations Variations { get; }

	public FlagEvaluationConfiguration(FlagIdentifier identifier,
		EvaluationModes? activeEvaluationModes = null,
		UtcSchedule? schedule = null,
		UtcTimeWindow? operationalWindow = null,
		List<ITargetingRule>? targetingRules = null,
		AccessControl? userAccessControl = null,
		AccessControl? tenantAccessControl = null,
		Variations? variations = null)
	{
		Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
		ActiveEvaluationModes = activeEvaluationModes ?? EvaluationModes.FlagIsDisabled;
		Schedule = schedule ?? UtcSchedule.Unscheduled;
		OperationalWindow = operationalWindow ?? UtcTimeWindow.AlwaysOpen;
		TargetingRules = targetingRules ?? [];
		UserAccessControl = userAccessControl ?? AccessControl.Unrestricted;
		TenantAccessControl = tenantAccessControl ?? AccessControl.Unrestricted;
		Variations = variations ?? Variations.OnOff;
	}

	public static FlagEvaluationConfiguration CreateGlobal(string key)
	{
		var identifier = new FlagIdentifier(key, Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		return new FlagEvaluationConfiguration(identifier);
	}
}
