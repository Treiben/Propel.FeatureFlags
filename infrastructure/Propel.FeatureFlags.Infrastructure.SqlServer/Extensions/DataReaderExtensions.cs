using Microsoft.Data.SqlClient;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Data;
using System.Text.Json;

namespace Propel.FeatureFlags.Infrastructure.SqlServer.Extensions;

public static class SqlDataReaderExtensions
{
	public static async Task<T> DeserializeAsync<T>(this SqlDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		if (await reader.IsDBNullAsync(ordinal))
			return default!;

		var json = await reader.GetFieldValueAsync<string>(ordinal);
		return JsonSerializer.Deserialize<T>(json, JsonDefaults.JsonOptions) ?? default!;
	}

	public static async Task<T> GetFieldValueOrDefaultAsync<T>(this SqlDataReader reader, string columnName, T defaultValue = default!)
	{
		var ordinal = reader.GetOrdinal(columnName);
		return await reader.IsDBNullAsync(ordinal)
			? defaultValue
			: await reader.GetFieldValueAsync<T>(ordinal);
	}

	public static async Task<FlagEvaluationConfiguration> LoadAsync(this SqlDataReader reader, FlagIdentifier identifier)
	{
		// Load evaluation modes
		var evaluationModes = await reader.DeserializeAsync<int[]>("EvaluationModes");
		var evaluationModeSet = new EvaluationModes([.. evaluationModes.Select(m => (EvaluationMode)m)]);

		// Load schedule - handle DB nulls properly
		var enableOn = await reader.GetFieldValueOrDefaultAsync<DateTimeOffset?>("ScheduledEnableDate");
		var disableOn = await reader.GetFieldValueOrDefaultAsync<DateTimeOffset?>("ScheduledDisableDate");
		
		var schedule = new ActivationSchedule(
			enableOn: enableOn?.DateTime ?? DateTime.MinValue,
			disableOn: disableOn?.DateTime ?? DateTime.MaxValue
		);

		// Load operational window
		var windowDaysData = await reader.DeserializeAsync<int[]>("WindowDays");
		var windowDays = windowDaysData?.Select(d => (DayOfWeek)d).ToArray();

		var operationalWindow = new OperationalWindow(
			startOn: await reader.GetFieldValueOrDefaultAsync<TimeSpan>("WindowStartTime"),
			stopOn: await reader.GetFieldValueOrDefaultAsync("WindowEndTime", new TimeSpan(23, 59, 59)),
			timeZone: await reader.GetFieldValueOrDefaultAsync("TimeZone", "UTC"),
			daysActive: windowDays
		);

		// Load access controls
		var userAccess = new AccessControl(
			allowed: await reader.DeserializeAsync<List<string>>("EnabledUsers"),
			blocked: await reader.DeserializeAsync<List<string>>("DisabledUsers"),
			rolloutPercentage: await reader.GetFieldValueAsync<int>("UserPercentageEnabled")
		);

		var tenantAccess = new AccessControl(
			allowed: await reader.DeserializeAsync<List<string>>("EnabledTenants"),
			blocked: await reader.DeserializeAsync<List<string>>("DisabledTenants"),
			rolloutPercentage: await reader.GetFieldValueAsync<int>("TenantPercentageEnabled")
		);

		// Load variations
		var variations = new Variations
		{
			Values = await reader.DeserializeAsync<Dictionary<string, object>>("Variations") ?? [],
			DefaultVariation = await reader.GetFieldValueOrDefaultAsync("DefaultVariation", "off")
		};

		// Load targeting rules
		var targetingRules = await reader.DeserializeAsync<List<ITargetingRule>>("TargetingRules") ?? [];

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