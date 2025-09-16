using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Data;
using System.Text.Json;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;

public static class NpgsqlDataReaderExtensions
{
	public static async Task<T> DeserializeAsync<T>(this NpgsqlDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		if (await reader.IsDBNullAsync(ordinal))
			return default!;

		var json = await reader.GetFieldValueAsync<string>(ordinal);
		return JsonSerializer.Deserialize<T>(json, JsonDefaults.JsonOptions) ?? default!;
	}

	public static async Task<T> GetFieldValueOrDefaultAsync<T>(this NpgsqlDataReader reader, string columnName, T defaultValue = default!)
	{
		var ordinal = reader.GetOrdinal(columnName);
		return await reader.IsDBNullAsync(ordinal)
			? defaultValue
			: await reader.GetFieldValueAsync<T>(ordinal);
	}

	public static async Task<FeatureFlag> LoadAllFields(this NpgsqlDataReader reader)
	{
		// Load flag key
		var flagKey = new FlagKey(
			key: await reader.GetFieldValueAsync<string>("key"),
			scope: (Scope)await reader.GetFieldValueAsync<int>("scope"),
			applicationName: await reader.GetFieldValueOrDefaultAsync<string?>("application_name"),
			applicationVersion: await reader.GetFieldValueOrDefaultAsync<string?>("application_version")
		);
		// Load evaluation modes
		var evaluationModes = await reader.DeserializeAsync<int[]>("evaluation_modes");
		var evaluationModeSet = new EvaluationModes([.. evaluationModes.Select(m => (EvaluationMode)m)]);

		// Load retention policy
		var retention = new RetentionPolicy(
			isPermanent: await reader.GetFieldValueAsync<bool>("is_permanent"),
			expirationDate: await reader.GetFieldValueOrDefaultAsync<DateTime?>("expiration_date") ?? DateTime.UtcNow.AddDays(30)
		);

		// Load audit information
		var created = new AuditTrail(
			timestamp: await reader.GetFieldValueAsync<DateTime>("created_at"),
			actor: await reader.GetFieldValueAsync<string>("created_by"),
			reason: await reader.GetFieldValueOrDefaultAsync<string?>("creation_reason")
		);

		AuditTrail? modified = null;
		var modifiedAt = await reader.GetFieldValueOrDefaultAsync<DateTime?>("updated_at");
		var modifiedBy = await reader.GetFieldValueOrDefaultAsync<string?>("updated_by");
		var modifiedReason = await reader.GetFieldValueOrDefaultAsync<string?>("modification_reason");
		if (modifiedAt.HasValue && !string.IsNullOrEmpty(modifiedBy))
		{
			modified = new AuditTrail(timestamp: modifiedAt.Value, actor: modifiedBy, modifiedReason);
		}

		// Load schedule - handle DB nulls properly
		var enableOn = await reader.GetFieldValueOrDefaultAsync<DateTime?>("scheduled_enable_date");
		var disableOn = await reader.GetFieldValueOrDefaultAsync<DateTime?>("scheduled_disable_date");

		var schedule = new ActivationSchedule(
			enableOn: enableOn ?? DateTime.MinValue,
			disableOn: disableOn ?? DateTime.MaxValue
		);

		// Load operational window
		var windowDaysData = await reader.DeserializeAsync<int[]>("window_days");
		var windowDays = windowDaysData?.Select(d => (DayOfWeek)d).ToArray();

		var operationalWindow = new OperationalWindow(
			startOn: await reader.GetFieldValueOrDefaultAsync<TimeSpan>("window_start_time"),
			stopOn: await reader.GetFieldValueOrDefaultAsync("window_end_time", new TimeSpan(23, 59, 59)),
			timeZone: await reader.GetFieldValueOrDefaultAsync("time_zone", "UTC"),
			daysActive: windowDays
		);

		// Load access controls
		var userAccess = new AccessControl(
			allowed: await reader.DeserializeAsync<List<string>>("enabled_users"),
			blocked: await reader.DeserializeAsync<List<string>>("disabled_users"),
			rolloutPercentage: await reader.GetFieldValueAsync<int>("user_percentage_enabled")
		);

		var tenantAccess = new AccessControl(
			allowed: await reader.DeserializeAsync<List<string>>("enabled_tenants"),
			blocked: await reader.DeserializeAsync<List<string>>("disabled_tenants"),
			rolloutPercentage: await reader.GetFieldValueAsync<int>("tenant_percentage_enabled")
		);

		// Load variations
		var variations = new Variations
		{
			Values = await reader.DeserializeAsync<Dictionary<string, object>>("variations") ?? [],
			DefaultVariation = await reader.GetFieldValueOrDefaultAsync("default_variation", "off")
		};

		return new FeatureFlag
		{
			Key = flagKey,
			Name = await reader.GetFieldValueAsync<string>("name"),
			Description = await reader.GetFieldValueAsync<string>("description"),
			ActiveEvaluationModes = evaluationModeSet,
			Retention = retention,
			Schedule = schedule,
			OperationalWindow = operationalWindow,
			UserAccessControl = userAccess,
			TenantAccessControl = tenantAccess,
			Variations = variations,
			TargetingRules = await reader.DeserializeAsync<List<ITargetingRule>>("targeting_rules") ?? [],
			Tags = await reader.DeserializeAsync<Dictionary<string, string>>("tags") ?? [],
			Created = created,
			LastModified = modified
		};
	}

	public static async Task<EvaluationCriteria> LoadOnlyEvalationFields(this NpgsqlDataReader reader)
	{
		// Load evaluation modes
		var evaluationModes = await reader.DeserializeAsync<int[]>("evaluation_modes");
		var evaluationModeSet = new EvaluationModes([.. evaluationModes.Select(m => (EvaluationMode)m)]);

		// Load schedule - handle DB nulls properly
		var enableOn = await reader.GetFieldValueOrDefaultAsync<DateTime?>("scheduled_enable_date");
		var disableOn = await reader.GetFieldValueOrDefaultAsync<DateTime?>("scheduled_disable_date");

		var schedule = new ActivationSchedule(
			enableOn: enableOn ?? DateTime.MinValue,
			disableOn: disableOn ?? DateTime.MaxValue
		);

		// Load operational window
		var windowDaysData = await reader.DeserializeAsync<int[]>("window_days");
		var windowDays = windowDaysData?.Select(d => (DayOfWeek)d).ToArray();

		var operationalWindow = new OperationalWindow(
			startOn: await reader.GetFieldValueOrDefaultAsync<TimeSpan>("window_start_time"),
			stopOn: await reader.GetFieldValueOrDefaultAsync("window_end_time", new TimeSpan(23, 59, 59)),
			timeZone: await reader.GetFieldValueOrDefaultAsync("time_zone", "UTC"),
			daysActive: windowDays
		);

		// Load access controls
		var userAccess = new AccessControl(
			allowed: await reader.DeserializeAsync<List<string>>("enabled_users"),
			blocked: await reader.DeserializeAsync<List<string>>("disabled_users"),
			rolloutPercentage: await reader.GetFieldValueAsync<int>("user_percentage_enabled")
		);

		var tenantAccess = new AccessControl(
			allowed: await reader.DeserializeAsync<List<string>>("enabled_tenants"),
			blocked: await reader.DeserializeAsync<List<string>>("disabled_tenants"),
			rolloutPercentage: await reader.GetFieldValueAsync<int>("tenant_percentage_enabled")
		);

		// Load variations
		var variations = new Variations
		{
			Values = await reader.DeserializeAsync<Dictionary<string, object>>("variations") ?? [],
			DefaultVariation = await reader.GetFieldValueOrDefaultAsync("default_variation", "off")
		};

		return new EvaluationCriteria
		{
			FlagKey = await reader.GetFieldValueAsync<string>("key"),
			ActiveEvaluationModes = evaluationModeSet,
			Schedule = schedule,
			OperationalWindow = operationalWindow,
			UserAccessControl = userAccess,
			TenantAccessControl = tenantAccess,
			Variations = variations,
			TargetingRules = await reader.DeserializeAsync<List<ITargetingRule>>("targeting_rules") ?? [],
		};
	}
}