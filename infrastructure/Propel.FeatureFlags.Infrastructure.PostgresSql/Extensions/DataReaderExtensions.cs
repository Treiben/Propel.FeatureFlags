using Knara.UtcStrict;
using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Data;
using System.Text.Json;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;

public static class DataReaderExtensions
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

	public static async Task<FlagEvaluationConfiguration> LoadAsync(this NpgsqlDataReader reader, FlagIdentifier identifier)
	{
		// Load evaluation modes
		var evaluationModes = await reader.DeserializeAsync<int[]>("evaluation_modes");
		var evaluationModeSet = new EvaluationModes([.. evaluationModes.Select(m => (EvaluationMode)m)]);

		// Load schedule - handle DB nulls properly
		var enableOn = await reader.GetFieldValueOrDefaultAsync<DateTimeOffset?>("scheduled_enable_date");
		var disableOn = await reader.GetFieldValueOrDefaultAsync<DateTimeOffset?>("scheduled_disable_date");

		var schedule = new UtcSchedule(
			enableOn: enableOn ?? UtcDateTime.MinValue,
			disableOn: disableOn ?? UtcDateTime.MaxValue
		);

		// Load operational window
		var windowDaysData = await reader.DeserializeAsync<int[]>("window_days");
		var windowDays = windowDaysData?.Select(d => (DayOfWeek)d).ToArray();

		var operationalWindow = new UtcTimeWindow(
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

		// Load targeting rules
		var targetingRules = await reader.DeserializeAsync<List<ITargetingRule>>("targeting_rules") ?? [];

		return new FlagEvaluationConfiguration(
			identifier: identifier,
			activeEvaluationModes: evaluationModeSet,
			schedule: schedule,
			operationalWindow: operationalWindow,
			userAccessControl: userAccess,
			tenantAccessControl: tenantAccess,
			variations: variations,
			targetingRules: targetingRules);
	}
}