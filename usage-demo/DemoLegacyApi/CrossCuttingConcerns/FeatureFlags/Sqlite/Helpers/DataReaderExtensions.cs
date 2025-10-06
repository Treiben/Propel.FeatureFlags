using Knara.UtcStrict;
using Microsoft.Data.Sqlite;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite.Helpers
{
	internal static class SqliteDataReaderExtensions
	{
		public static async Task<T> DeserializeAsync<T>(this SqliteDataReader reader, string columnName)
		{
			var ordinal = reader.GetOrdinal(columnName);
			if (await reader.IsDBNullAsync(ordinal))
				return default!;

			var json = reader.GetString(ordinal);
			return JsonSerializer.Deserialize<T>(json, JsonDefaults.JsonOptions) ?? default!;
		}

		internal static async Task<T> GetFieldValueOrDefaultAsync<T>(this SqliteDataReader reader, string columnName, T defaultValue = default!)
		{
			var ordinal = reader.GetOrdinal(columnName);
			if (await reader.IsDBNullAsync(ordinal))
				return defaultValue;

			// SQLite stores everything as TEXT, INTEGER, REAL, or BLOB
			// Handle type conversions explicitly
			var type = typeof(T);

			if (type == typeof(string))
				return (T)(object)reader.GetString(ordinal);

			if (type == typeof(int) || type == typeof(int?))
				return (T)(object)reader.GetInt32(ordinal);

			if (type == typeof(long) || type == typeof(long?))
				return (T)(object)reader.GetInt64(ordinal);

			if (type == typeof(bool) || type == typeof(bool?))
				return (T)(object)(reader.GetInt32(ordinal) != 0);

			if (type == typeof(DateTimeOffset) || type == typeof(DateTimeOffset?))
			{
				var text = reader.GetString(ordinal);
				return (T)(object)DateTimeOffset.Parse(text);
			}

			if (type == typeof(TimeSpan) || type == typeof(TimeSpan?))
			{
				var text = reader.GetString(ordinal);
				return (T)(object)TimeSpan.Parse(text);
			}

			if (type == typeof(Guid) || type == typeof(Guid?))
			{
				var text = reader.GetString(ordinal);
				return (T)(object)Guid.Parse(text);
			}

			// Fallback to GetValue for other types
			return (T)reader.GetValue(ordinal);
		}

		internal static async Task<EvaluationOptions> LoadOptionsAsync(this SqliteDataReader reader, FlagIdentifier identifier)
		{
			// Load evaluation modes
			var evaluationModes = await reader.DeserializeAsync<int[]>("EvaluationModes");
			var evaluationModeSet = new ModeSet(new HashSet<EvaluationMode>(evaluationModes.Select(m => (EvaluationMode)m)));

			// Load schedule - handle DB nulls properly
			// SQLite stores DateTimeOffset as TEXT in ISO 8601 format
			var enableOn = await reader.GetFieldValueOrDefaultAsync<DateTimeOffset?>("ScheduledEnableDate");
			var disableOn = await reader.GetFieldValueOrDefaultAsync<DateTimeOffset?>("ScheduledDisableDate");

			var schedule = new UtcSchedule(
				enableOn: enableOn ?? UtcDateTime.MinValue,
				disableOn: disableOn ?? UtcDateTime.MaxValue
			);

			// Load operational window
			var windowDaysData = await reader.DeserializeAsync<int[]>("WindowDays");
			var windowDays = windowDaysData?.Select(d => (DayOfWeek)d).ToArray();

			var operationalWindow = new UtcTimeWindow(
				startOn: await reader.GetFieldValueOrDefaultAsync<TimeSpan>("WindowStartTime"),
				stopOn: await reader.GetFieldValueOrDefaultAsync("WindowEndTime", new TimeSpan(23, 59, 59)),
				timeZone: await reader.GetFieldValueOrDefaultAsync("TimeZone", "UTC"),
				daysActive: windowDays
			);

			// Load access controls
			var userAccess = new AccessControl(
				allowed: await reader.DeserializeAsync<List<string>>("EnabledUsers"),
				blocked: await reader.DeserializeAsync<List<string>>("DisabledUsers"),
				rolloutPercentage: await reader.GetFieldValueOrDefaultAsync<int>("UserPercentageEnabled")
			);

			var tenantAccess = new AccessControl(
				allowed: await reader.DeserializeAsync<List<string>>("EnabledTenants"),
				blocked: await reader.DeserializeAsync<List<string>>("DisabledTenants"),
				rolloutPercentage: await reader.GetFieldValueOrDefaultAsync<int>("TenantPercentageEnabled")
			);

			// Load variations
			var variations = new Variations
			{
				Values = await reader.DeserializeAsync<Dictionary<string, object>>("Variations") ?? new Dictionary<string, object>(),
				DefaultVariation = await reader.GetFieldValueOrDefaultAsync("DefaultVariation", "off")
			};

			// Load targeting rules
			var targetingRules = await reader.DeserializeAsync<List<ITargetingRule>>("TargetingRules") ?? new List<ITargetingRule>();

			return new EvaluationOptions(
				key: identifier.Key,
				modeSet: evaluationModeSet,
				schedule: schedule,
				operationalWindow: operationalWindow,
				userAccessControl: userAccess,
				tenantAccessControl: tenantAccess,
				variations: variations,
				targetingRules: targetingRules);
		}
	}
}