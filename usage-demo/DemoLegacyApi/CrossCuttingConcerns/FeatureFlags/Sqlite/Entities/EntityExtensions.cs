using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite.Entities
{
	internal static class EntityExtensions
	{
		public static EvaluationOptions ToEvaluationOptions(this FeatureFlagEntity entity, FlagIdentifier identifier)
		{
			// Load evaluation modes
			var evaluationModes = JsonSerializer.Deserialize<int[]>(entity.EvaluationModes, JsonDefaults.JsonOptions) ?? Array.Empty<int>();
			var evaluationModeSet = new ModeSet(evaluationModes.Select(m => (EvaluationMode)m).ToHashSet());

			// Load schedule
			var schedule = new UtcSchedule(
				enableOn: entity.ScheduledEnableDate ?? UtcDateTime.MinValue,
				disableOn: entity.ScheduledDisableDate ?? UtcDateTime.MaxValue
			);

			// Load operational window
			var windowDaysData = JsonSerializer.Deserialize<int[]>(entity.WindowDays, JsonDefaults.JsonOptions);
			var windowDays = windowDaysData?.Select(d => (DayOfWeek)d).ToArray();

			var operationalWindow = new UtcTimeWindow(
				startOn: entity.WindowStartTime ?? TimeSpan.Zero,
				stopOn: entity.WindowEndTime ?? new TimeSpan(23, 59, 59),
				timeZone: entity.TimeZone ?? "UTC",
				daysActive: windowDays
			);

			// Load access controls
			var userAccess = new AccessControl(
				allowed: JsonSerializer.Deserialize<List<string>>(entity.EnabledUsers, JsonDefaults.JsonOptions),
				blocked: JsonSerializer.Deserialize<List<string>>(entity.DisabledUsers, JsonDefaults.JsonOptions),
				rolloutPercentage: entity.UserPercentageEnabled
			);

			var tenantAccess = new AccessControl(
				allowed: JsonSerializer.Deserialize<List<string>>(entity.EnabledTenants, JsonDefaults.JsonOptions),
				blocked: JsonSerializer.Deserialize<List<string>>(entity.DisabledTenants, JsonDefaults.JsonOptions),
				rolloutPercentage: entity.TenantPercentageEnabled
			);

			// Load variations
			var variations = new Variations
			{
				Values = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Variations, JsonDefaults.JsonOptions) ?? new Dictionary<string, object>(),
				DefaultVariation = entity.DefaultVariation ?? "off"
			};

			// Load targeting rules
			var targetingRules = JsonSerializer.Deserialize<List<ITargetingRule>>(entity.TargetingRules, JsonDefaults.JsonOptions) ?? new List<ITargetingRule>();

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