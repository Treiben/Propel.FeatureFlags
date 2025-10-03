using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Domain;

public record FeatureFlag(
	FlagIdentifier Identifier,
	FlagAdministration Administration,
	FlagEvaluationOptions EvaluationOptions);

public record FlagEvaluationOptions(
	ModeSet ModeSet,
	UtcSchedule Schedule,
	UtcTimeWindow OperationalWindow,
	List<ITargetingRule> TargetingRules,
	AccessControl UserAccessControl,
	AccessControl TenantAccessControl,
	Variations Variations)
{
	public static FlagEvaluationOptions DefaultOptions => new(
		ModeSet: EvaluationMode.Off,
		Schedule: UtcSchedule.Unscheduled,
		OperationalWindow: UtcTimeWindow.AlwaysOpen,
		TargetingRules: [],
		UserAccessControl: AccessControl.Unrestricted,
		TenantAccessControl: AccessControl.Unrestricted,
		Variations: new Variations());
}
