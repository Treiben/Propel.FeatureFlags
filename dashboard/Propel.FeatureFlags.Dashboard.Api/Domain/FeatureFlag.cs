using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Domain;

public record FeatureFlag(
	FlagIdentifier Identifier,
	Metadata Metadata,
	EvalConfiguration EvalConfig);

public record EvalConfiguration(
	EvaluationModes Modes,
	UtcSchedule Schedule,
	UtcTimeWindow OperationalWindow,
	List<ITargetingRule> TargetingRules,
	AccessControl UserAccessControl,
	AccessControl TenantAccessControl,
	Variations Variations)
{
	public static EvalConfiguration DefaultConfiguration => new(
		Modes: EvaluationModes.FlagIsDisabled,
		Schedule: UtcSchedule.Unscheduled,
		OperationalWindow: UtcTimeWindow.AlwaysOpen,
		TargetingRules: [],
		UserAccessControl: AccessControl.Unrestricted,
		TenantAccessControl: AccessControl.Unrestricted,
		Variations: Variations.OnOff);
}
