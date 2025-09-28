using Knara.UtcStrict;
using Npgsql;
using NpgsqlTypes;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure;
using System.Text.Json;
using Testcontainers.PostgreSql;

namespace FeatureFlags.IntegrationTests.Postgres.Support;

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

public static class PostgresDbHelpers
{
	public async static Task CreateFlagAsync(PostgreSqlContainer container, FlagEvaluationConfiguration flag,
		string name, string description)
	{
		var connectionString = container.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		using var command = new NpgsqlCommand(@"INSERT INTO feature_flags (
                key, application_name, application_version, scope,
				name, description, evaluation_modes, 
                scheduled_enable_date, scheduled_disable_date,
                window_start_time, window_end_time, time_zone, window_days,
				targeting_rules, variations, default_variation,
                user_percentage_enabled, enabled_users, disabled_users,
                tenant_percentage_enabled, enabled_tenants, disabled_tenants
            ) VALUES (
                @key, @application_name, @application_version, @scope,
				@name, @description, @evaluation_modes,
                @scheduled_enable_date, @scheduled_disable_date,
                @window_start_time, @window_end_time, @time_zone, @window_days,
				@targeting_rules, @variations, @default_variation,
                @user_percentage_enabled, @enabled_users, @disabled_users,
                @tenant_percentage_enabled, @enabled_tenants, @disabled_tenants            
            );", connection);

		command.AddFlagFieldsAsync(flag, name, description);

		await connection.OpenAsync();
		await command.ExecuteNonQueryAsync();
	}

	private static void AddFlagFieldsAsync(this NpgsqlCommand command, FlagEvaluationConfiguration flag,
		string name, string description)
	{
		// Flag identity parameters
		command.Parameters.AddWithValue("key", flag.Identifier.Key);
		command.Parameters.AddWithValue("application_name", (object?)flag.Identifier.ApplicationName ?? DBNull.Value);
		command.Parameters.AddWithValue("application_version", (object?)flag.Identifier.ApplicationVersion ?? DBNull.Value);
		command.Parameters.AddWithValue("scope", (int)flag.Identifier.Scope);

		command.Parameters.AddWithValue("name", name);
		command.Parameters.AddWithValue("description", description);


		// Schedule parameters - handle domain model defaults
		if (flag.Schedule.EnableOn == DateTime.MinValue)
			command.Parameters.AddWithValue("scheduled_enable_date", DBNull.Value);
		else
			command.Parameters.AddWithValue("scheduled_enable_date", flag.Schedule.EnableOn.DateTime);

		if (flag.Schedule.DisableOn == DateTime.MaxValue)
			command.Parameters.AddWithValue("scheduled_disable_date", DBNull.Value);
		else
			command.Parameters.AddWithValue("scheduled_disable_date", flag.Schedule.DisableOn.DateTime);

		// Operational window parameters
		command.Parameters.AddWithValue("window_start_time", flag.OperationalWindow.StartOn);
		command.Parameters.AddWithValue("window_end_time", flag.OperationalWindow.StopOn);
		command.Parameters.AddWithValue("time_zone", flag.OperationalWindow.TimeZone);

		// Access control parameters
		command.Parameters.AddWithValue("user_percentage_enabled", flag.UserAccessControl.RolloutPercentage);
		command.Parameters.AddWithValue("tenant_percentage_enabled", flag.TenantAccessControl.RolloutPercentage);

		// Variations
		command.Parameters.AddWithValue("default_variation", flag.Variations.DefaultVariation);

		// JSONB parameters require explicit type specification
		var evaluationModesParam = command.Parameters.Add("evaluation_modes", NpgsqlDbType.Jsonb);
		evaluationModesParam.Value = JsonSerializer.Serialize(flag.ActiveEvaluationModes.Modes.Select(m => (int)m), JsonDefaults.JsonOptions);

		var windowDaysParam = command.Parameters.Add("window_days", NpgsqlDbType.Jsonb);
		windowDaysParam.Value = JsonSerializer.Serialize(flag.OperationalWindow.DaysActive.Select(d => (int)d), JsonDefaults.JsonOptions);

		var targetingRulesParam = command.Parameters.Add("targeting_rules", NpgsqlDbType.Jsonb);
		targetingRulesParam.Value = JsonSerializer.Serialize(flag.TargetingRules, JsonDefaults.JsonOptions);

		var enabledUsersParam = command.Parameters.Add("enabled_users", NpgsqlDbType.Jsonb);
		enabledUsersParam.Value = JsonSerializer.Serialize(flag.UserAccessControl.Allowed, JsonDefaults.JsonOptions);

		var disabledUsersParam = command.Parameters.Add("disabled_users", NpgsqlDbType.Jsonb);
		disabledUsersParam.Value = JsonSerializer.Serialize(flag.UserAccessControl.Blocked, JsonDefaults.JsonOptions);

		var enabledTenantsParam = command.Parameters.Add("enabled_tenants", NpgsqlDbType.Jsonb);
		enabledTenantsParam.Value = JsonSerializer.Serialize(flag.TenantAccessControl.Allowed, JsonDefaults.JsonOptions);

		var disabledTenantsParam = command.Parameters.Add("disabled_tenants", NpgsqlDbType.Jsonb);
		disabledTenantsParam.Value = JsonSerializer.Serialize(flag.TenantAccessControl.Blocked, JsonDefaults.JsonOptions);

		var variationsParam = command.Parameters.Add("variations", NpgsqlDbType.Jsonb);
		variationsParam.Value = JsonSerializer.Serialize(flag.Variations.Values, JsonDefaults.JsonOptions);
	}
}
