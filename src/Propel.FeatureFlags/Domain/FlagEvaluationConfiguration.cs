namespace Propel.FeatureFlags.Domain;

public class FlagEvaluationConfiguration
{
	public FlagIdentifier Identifier { get; } 

	public EvaluationModes ActiveEvaluationModes { get; } = EvaluationModes.FlagIsDisabled;

	public ActivationSchedule Schedule { get;} = ActivationSchedule.Unscheduled;
	public OperationalWindow OperationalWindow { get; } = OperationalWindow.AlwaysOpen;

	// Targeting
	public List<ITargetingRule> TargetingRules { get; } = [];

	public AccessControl UserAccessControl { get; } = AccessControl.Unrestricted;
	public AccessControl TenantAccessControl { get; } = AccessControl.Unrestricted;

	// Variations for A/B testing
	public Variations Variations { get; } = Variations.OnOff;

	public FlagEvaluationConfiguration(FlagIdentifier identifier,
		EvaluationModes? activeEvaluationModes = null,
		ActivationSchedule? schedule = null,
		OperationalWindow? operationalWindow = null,
		List<ITargetingRule>? targetingRules = null,
		AccessControl? userAccessControl = null,
		AccessControl? tenantAccessControl = null,
		Variations? variations = null)
	{
		Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
		ActiveEvaluationModes = activeEvaluationModes ?? EvaluationModes.FlagIsDisabled;
		Schedule = schedule ?? ActivationSchedule.Unscheduled;
		OperationalWindow = operationalWindow ?? OperationalWindow.AlwaysOpen;
		TargetingRules = targetingRules ?? [];
		UserAccessControl = userAccessControl ?? AccessControl.Unrestricted;
		TenantAccessControl = tenantAccessControl ?? AccessControl.Unrestricted;
		Variations = variations ?? Variations.OnOff;
	}
}
