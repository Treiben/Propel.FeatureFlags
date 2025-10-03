using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace FeatureFlags.IntegrationTests.Redis.Support;

public class ApplicationFeatureFlag(
	string key,
	string? name = null,
	string? description = null,
	EvaluationMode defaultMode = EvaluationMode.Off)
	: FeatureFlagBase(key, name, description, defaultMode)
{
}

public class FlagConfigurationBuilder
{
	private FlagIdentifier _flagIdentifier;

	private List<ITargetingRule> _targetingRules = [];
	private ModeSet _evaluationModes = EvaluationMode.Off;
	private Variations _variations = new Variations();
	private UtcSchedule _schedule = UtcSchedule.Unscheduled;
	private UtcTimeWindow _window = UtcTimeWindow.AlwaysOpen;
	private AccessControl _userAccessControl = AccessControl.Unrestricted;
	private AccessControl _tenantAccessControl = AccessControl.Unrestricted;

	private ApplicationFeatureFlag? _featureFlag;

	public FlagConfigurationBuilder(string? key = null, Scope? scope = null)
	{
		var identifierKey = key ?? "default-flag";
		var identifierScope = scope ?? Scope.Application;
		if (identifierScope == Scope.Application)
		{
			_flagIdentifier = new FlagIdentifier(identifierKey, identifierScope, ApplicationInfo.Name, ApplicationInfo.Version);
		}
		else // Global or Feature scope
			_flagIdentifier = new FlagIdentifier(identifierKey, identifierScope);
	}

	public FlagConfigurationBuilder WithEvaluationModes(params EvaluationMode[] modes)
	{
		_evaluationModes = new ModeSet([.. modes]);
		return this;
	}

	public FlagConfigurationBuilder WithTargetingRules(List<ITargetingRule> rules)
	{
		_targetingRules = rules;
		return this;
	}

	public FlagConfigurationBuilder WithVariations(Variations variations)
	{
		_variations = variations;
		return this;
	}

	public FlagConfigurationBuilder WithSchedule(UtcSchedule schedule)
	{
		_schedule = schedule;
		return this;
	}

	public FlagConfigurationBuilder WithOperationalWindow(UtcTimeWindow window)
	{
		_window = window;
		return this;
	}

	public FlagConfigurationBuilder WithUserAccessControl(AccessControl accessControl)
	{
		_userAccessControl = accessControl;
		return this;
	}

	public FlagConfigurationBuilder WithTenantAccessControl(AccessControl accessControl)
	{
		_tenantAccessControl = accessControl;
		return this;
	}

	public FlagConfigurationBuilder ForFeatureFlag(string? name = null, string? description = null, EvaluationMode defaultMode = EvaluationMode.Off)
	{
		_featureFlag = new ApplicationFeatureFlag(
			key: _flagIdentifier.Key,
			name: name ?? $"App Flag {_flagIdentifier.Key}",
			description: description ?? "Application flag for integration tests",
			defaultMode: defaultMode);
		return this;
	}

	public (EvaluationOptions, IFeatureFlag?) Build()
	{
		var flagConfig = new EvaluationOptions(
			key: _flagIdentifier.Key,
			modeSet: _evaluationModes,
			schedule: _schedule,
			operationalWindow: _window,
			targetingRules: _targetingRules,
			userAccessControl: _userAccessControl,
			tenantAccessControl: _tenantAccessControl,
			variations: _variations);

		return (flagConfig, _featureFlag);
	}
}

public static class CacheKeyFactory
{
	public static CacheKey CreateCacheKey(string key)
	{
		var applicationName = ApplicationInfo.Name;
		var applicationVersion = ApplicationInfo.Version;

		return new CacheKey(key, [applicationName, applicationVersion]);
	}

	public static CacheKey CreateGlobalCacheKey(string key)
	{
		return new CacheKey(key, ["global"]);
	}
}
