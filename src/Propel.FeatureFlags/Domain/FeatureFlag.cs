namespace Propel.FeatureFlags.Domain;

/// <summary>
/// Full flag definition including targeting rules, variations, and metadata
/// </summary>
public class FeatureFlag
{
	public FlagKey Key { get; set; } 
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;

	public EvaluationModes ActiveEvaluationModes { get; set; } = EvaluationModes.FlagIsDisabled;

	public ActivationSchedule Schedule { get; set; } = ActivationSchedule.Unscheduled;
	public OperationalWindow OperationalWindow { get; set; } = OperationalWindow.AlwaysOpen;
	public RetentionPolicy Retention { get; set; } = RetentionPolicy.ApplicationDefault;

	// Targeting
	public List<ITargetingRule> TargetingRules { get; set; } = [];

	public AccessControl UserAccessControl { get; set; } = AccessControl.Unrestricted;
	public AccessControl TenantAccessControl { get; set; } = AccessControl.Unrestricted;

	// Variations for A/B testing
	public Variations Variations { get; set; } = Variations.OnOff;

	// Metadata
	public Dictionary<string, string> Tags { get; set; } = [];
	public AuditTrail Created { get; set; } = AuditTrail.FlagCreated();
	public AuditTrail? LastModified { get; set; } = default;

	public static FeatureFlag Create(FlagKey key, string name, string description)
	{
		var flag = new FeatureFlag
		{
			Key = key ?? throw new ArgumentNullException(nameof(key)),
			Name = name ?? throw new ArgumentNullException(nameof(name)),
			Description = description ?? string.Empty,
			Retention = key.Scope == Scope.Global ? RetentionPolicy.Global : RetentionPolicy.ApplicationDefault
		};

		return flag;
	}
}