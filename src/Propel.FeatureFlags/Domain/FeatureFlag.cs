namespace Propel.FeatureFlags.Domain;

public class FeatureFlag
{
	public string Key { get; set; } = string.Empty;
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
	public Audit Created { get; set; } = Audit.FlagCreated();
	public Audit? LastModified { get; set; } = default;

	public static FeatureFlag Create(string key, string name, string description)
	{
		var flag = new FeatureFlag
		{
			Key = key ?? throw new ArgumentNullException(nameof(key)),
			Name = name ?? throw new ArgumentNullException(nameof(name)),
			Description = description ?? string.Empty
		};

		return flag;
	}
}
