using Knara.UtcStrict;
using Microsoft.Data.SqlClient;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure;
using System.Text.Json;
using Testcontainers.MsSql;

namespace FeatureFlags.IntegrationTests.SqlServer.Support;

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
	private EvaluationModes _evaluationModes = EvaluationModes.FlagIsDisabled;
	private Variations _variations = Variations.OnOff;
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
		_evaluationModes = new EvaluationModes([.. modes]);
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

	public (FlagEvaluationConfiguration, IFeatureFlag?) Build()
	{
		var flagConfig = new FlagEvaluationConfiguration(
			identifier: _flagIdentifier,
			activeEvaluationModes: _evaluationModes,
			schedule: _schedule,
			operationalWindow: _window,
			targetingRules: _targetingRules,
			userAccessControl: _userAccessControl,
			tenantAccessControl: _tenantAccessControl,
			variations: _variations);

		return (flagConfig, _featureFlag);
	}
}

public static class SqlServerDbHelpers
{
	public async static Task CreateFlagAsync(MsSqlContainer container, FlagEvaluationConfiguration flag,
		string name, string description)
	{
		var connectionString = container.GetConnectionString();
		using var connection = new SqlConnection(connectionString);
		using var command = new SqlCommand(@"INSERT INTO FeatureFlags (
                [Key], ApplicationName, ApplicationVersion, Scope,
				Name, Description, EvaluationModes, 
                ScheduledEnableDate, ScheduledDisableDate,
                WindowStartTime, WindowEndTime, TimeZone, WindowDays,
				TargetingRules, Variations, DefaultVariation,
                UserPercentageEnabled, EnabledUsers, DisabledUsers,
                TenantPercentageEnabled, EnabledTenants, DisabledTenants
            ) VALUES (
                @Key, @ApplicationName, @ApplicationVersion, @Scope,
				@Name, @Description, @EvaluationModes,
                @ScheduledEnableDate, @ScheduledDisableDate,
                @WindowStartTime, @WindowEndTime, @TimeZone, @WindowDays,
				@TargetingRules, @Variations, @DefaultVariation,
                @UserPercentageEnabled, @EnabledUsers, @DisabledUsers,
                @TenantPercentageEnabled, @EnabledTenants, @DisabledTenants            
            );", connection);

		command.AddFlagFieldsAsync(flag, name, description);

		await connection.OpenAsync();
		await command.ExecuteNonQueryAsync();
	}

	private static void AddFlagFieldsAsync(this SqlCommand command, FlagEvaluationConfiguration flag,
		string name, string description)
	{
		// Flag identity parameters
		command.Parameters.AddWithValue("Key", flag.Identifier.Key);
		command.Parameters.AddWithValue("ApplicationName", (object?)flag.Identifier.ApplicationName ?? DBNull.Value);
		command.Parameters.AddWithValue("ApplicationVersion", (object?)flag.Identifier.ApplicationVersion ?? DBNull.Value);
		command.Parameters.AddWithValue("Scope", (int)flag.Identifier.Scope);

		command.Parameters.AddWithValue("Name", name);
		command.Parameters.AddWithValue("Description", description);

		command.Parameters.AddWithValue("EvaluationModes",
			JsonSerializer.Serialize(flag.ActiveEvaluationModes.Modes.Select(m => (int)m), JsonDefaults.JsonOptions));

		// Schedule parameters - handle domain model defaults
		if (flag.Schedule.EnableOn == DateTime.MinValue.ToUniversalTime())
			command.Parameters.AddWithValue("ScheduledEnableDate", DBNull.Value);
		else
			command.Parameters.AddWithValue("ScheduledEnableDate", (DateTimeOffset)flag.Schedule.EnableOn);

		if (flag.Schedule.DisableOn == DateTime.MaxValue.ToUniversalTime())
			command.Parameters.AddWithValue("ScheduledDisableDate", DBNull.Value);
		else
			command.Parameters.AddWithValue("ScheduledDisableDate", (DateTimeOffset)flag.Schedule.DisableOn);

		// Operational window parameters
		command.Parameters.AddWithValue("WindowStartTime", flag.OperationalWindow.StartOn);
		command.Parameters.AddWithValue("WindowEndTime", flag.OperationalWindow.StopOn);
		command.Parameters.AddWithValue("TimeZone", flag.OperationalWindow.TimeZone);
		command.Parameters.AddWithValue("WindowDays",
			JsonSerializer.Serialize(flag.OperationalWindow.DaysActive.Select(d => (int)d), JsonDefaults.JsonOptions));


		// Variations
		command.Parameters.AddWithValue("DefaultVariation", flag.Variations.DefaultVariation);
		command.Parameters.AddWithValue("Variations",
			JsonSerializer.Serialize(flag.Variations.Values, JsonDefaults.JsonOptions));

		// Targeting rules
		command.Parameters.AddWithValue("TargetingRules",
			JsonSerializer.Serialize(flag.TargetingRules, JsonDefaults.JsonOptions));

		// Access control parameters
		command.Parameters.AddWithValue("UserPercentageEnabled", flag.UserAccessControl.RolloutPercentage);
		command.Parameters.AddWithValue("EnabledUsers",
			JsonSerializer.Serialize(flag.UserAccessControl.Allowed, JsonDefaults.JsonOptions));
		command.Parameters.AddWithValue("DisabledUsers",
			JsonSerializer.Serialize(flag.UserAccessControl.Blocked, JsonDefaults.JsonOptions));

		// Access control parameters
		command.Parameters.AddWithValue("TenantPercentageEnabled", flag.TenantAccessControl.RolloutPercentage);
		command.Parameters.AddWithValue("EnabledTenants",
			JsonSerializer.Serialize(flag.TenantAccessControl.Allowed, JsonDefaults.JsonOptions));
		command.Parameters.AddWithValue("DisabledTenants",
			JsonSerializer.Serialize(flag.TenantAccessControl.Blocked, JsonDefaults.JsonOptions));


	}
}
