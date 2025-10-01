using Knara.UtcStrict;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Domain;

namespace FeatureFlags.IntegrationTests.SqlServer.Support;

public class ApplicationFeatureFlag(
	string key,
	string? name = null,
	string? description = null,
	EvaluationMode defaultMode = EvaluationMode.Off)
	: FeatureFlagBase(key, name, description, defaultMode)
{
}

public class FlagOptionsBuilder
{
	private List<ITargetingRule> _targetingRules = [];
	private ModeSet _evaluationModes = EvaluationMode.Off;
	private Variations _variations = Variations.OnOff;
	private UtcSchedule _schedule = UtcSchedule.Unscheduled;
	private UtcTimeWindow _window = UtcTimeWindow.AlwaysOpen;
	private AccessControl _userAccessControl = AccessControl.Unrestricted;
	private AccessControl _tenantAccessControl = AccessControl.Unrestricted;

	public FlagOptionsBuilder WithEvaluationModes(params EvaluationMode[] modes)
	{
		_evaluationModes = new ModeSet([.. modes]);
		return this;
	}

	public FlagOptionsBuilder WithTargetingRules(List<ITargetingRule> rules)
	{
		_targetingRules = rules;
		return this;
	}

	public FlagOptionsBuilder WithVariations(Variations variations)
	{
		_variations = variations;
		return this;
	}

	public FlagOptionsBuilder WithSchedule(UtcSchedule schedule)
	{
		_schedule = schedule;
		return this;
	}

	public FlagOptionsBuilder WithOperationalWindow(UtcTimeWindow window)
	{
		_window = window;
		return this;
	}

	public FlagOptionsBuilder WithUserAccessControl(AccessControl accessControl)
	{
		_userAccessControl = accessControl;
		return this;
	}

	public FlagOptionsBuilder WithTenantAccessControl(AccessControl accessControl)
	{
		_tenantAccessControl = accessControl;
		return this;
	}

	public FlagEvaluationOptions Build()
	{
		return new FlagEvaluationOptions(
			ModeSet: _evaluationModes,
			Schedule: _schedule,
			OperationalWindow: _window,
			TargetingRules: _targetingRules,
			UserAccessControl: _userAccessControl,
			TenantAccessControl: _tenantAccessControl,
			Variations: _variations);
	}
}
