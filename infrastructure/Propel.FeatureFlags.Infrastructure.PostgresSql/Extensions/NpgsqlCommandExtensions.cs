using Npgsql;
using NpgsqlTypes;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Data;
using System.Text.Json;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;

public static class NpgsqlCommandExtensions
{
	public static void AddAllParameters(this NpgsqlCommand command, FeatureFlag flag)
	{
		// Flag identity parameters
		command.Parameters.AddWithValue("key", flag.Key.Key);
		command.Parameters.AddWithValue("application_name", (object?)flag.Key.ApplicationName ?? DBNull.Value);
		command.Parameters.AddWithValue("application_version", (object?)flag.Key.ApplicationVersion ?? DBNull.Value);
		command.Parameters.AddWithValue("scope", (int)flag.Key.Scope);

		command.Parameters.AddWithValue("name", flag.Name);
		command.Parameters.AddWithValue("description", flag.Description);

		// Retention parameters
		command.Parameters.AddWithValue("expiration_date", (object?)flag.Retention.ExpirationDate ?? DBNull.Value);
		command.Parameters.AddWithValue("is_permanent", flag.Retention.IsPermanent);

		// Schedule parameters - handle domain model defaults
		if (flag.Schedule.EnableOn == DateTime.MinValue)
			command.Parameters.AddWithValue("scheduled_enable_date", DBNull.Value);
		else
			command.Parameters.AddWithValue("scheduled_enable_date", flag.Schedule.EnableOn);

		if (flag.Schedule.DisableOn == DateTime.MaxValue)
			command.Parameters.AddWithValue("scheduled_disable_date", DBNull.Value);
		else
			command.Parameters.AddWithValue("scheduled_disable_date", flag.Schedule.DisableOn);

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

		var tagsParam = command.Parameters.Add("tags", NpgsqlDbType.Jsonb);
		tagsParam.Value = JsonSerializer.Serialize(flag.Tags, JsonDefaults.JsonOptions);
	}

	public static void AddRequiredParameters(this NpgsqlCommand command, FeatureFlag flag)
	{
		// Basic string parameters
		command.Parameters.AddWithValue("key", flag.Key.Key);
		command.Parameters.AddWithValue("name", flag.Name);
		command.Parameters.AddWithValue("description", flag.Description);

		var evaluationModesParam = command.Parameters.Add("evaluation_modes", NpgsqlDbType.Jsonb);
		evaluationModesParam.Value = JsonSerializer.Serialize(flag.ActiveEvaluationModes.Modes.Select(m => (int)m), JsonDefaults.JsonOptions);

		command.Parameters.AddWithValue("expiration_date", (object?)flag.Retention.ExpirationDate ?? DBNull.Value);
		command.Parameters.AddWithValue("is_permanent", flag.Retention.IsPermanent);

		command.Parameters.AddWithValue("application_name", (object?)flag.Key.ApplicationName ?? DBNull.Value);
		command.Parameters.AddWithValue("application_version", (object?)flag.Key.ApplicationVersion ?? DBNull.Value);
		command.Parameters.AddWithValue("scope", (int)flag.Key.Scope);

		command.Parameters.AddWithValue("timestamp", flag.Created.Timestamp);
	}

	public static void AddAuditParameters(this NpgsqlCommand command, FlagKey flag, string action, string? actor, string? reason = null)
	{
		command.Parameters.AddWithValue("key", flag.Key);
		command.Parameters.AddWithValue("application_name", (object?)flag.ApplicationName ?? DBNull.Value);
		command.Parameters.AddWithValue("application_version", (object?)flag.ApplicationVersion ?? DBNull.Value);
		command.Parameters.AddWithValue("action", action);
		command.Parameters.AddWithValue("actor", actor ?? "system");
		command.Parameters.AddWithValue("timestamp", DateTime.UtcNow);
		command.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
	}

	public static void AddFilterParameters(this NpgsqlCommand command, Dictionary<string, object> parameters)
	{
		foreach (var (key, value) in parameters)
		{
			if (key.StartsWith("tag") && !key.StartsWith("tagKey"))
			{
				// JSONB parameter for tag values
				var parameter = command.Parameters.Add(key, NpgsqlDbType.Jsonb);
				parameter.Value = value;
			}
			else if (key.StartsWith("mode"))
			{
				// JSONB parameter for evaluation modes
				var parameter = command.Parameters.Add(key, NpgsqlDbType.Jsonb);
				parameter.Value = value;
			}
			else
			{
				command.Parameters.AddWithValue(key, value);
			}
		}
	}
}
