namespace Propel.FeatureFlags.Domain;

/// <summary>
/// Flag definition used during evaluation, stripped of metadata and audit fields for efficiency
/// </summary>
public class EvaluationCriteria
{
	public string FlagKey { get; set; } = string.Empty;

	public EvaluationModes ActiveEvaluationModes { get; set; } = EvaluationModes.FlagIsDisabled;

	public ActivationSchedule Schedule { get; set; } = ActivationSchedule.Unscheduled;
	public OperationalWindow OperationalWindow { get; set; } = OperationalWindow.AlwaysOpen;

	// Targeting
	public List<ITargetingRule> TargetingRules { get; set; } = [];

	public AccessControl UserAccessControl { get; set; } = AccessControl.Unrestricted;
	public AccessControl TenantAccessControl { get; set; } = AccessControl.Unrestricted;

	// Variations for A/B testing
	public Variations Variations { get; set; } = Variations.OnOff;
}
