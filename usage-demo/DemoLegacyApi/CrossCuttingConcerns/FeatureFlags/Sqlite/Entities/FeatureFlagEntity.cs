using System;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite.Entities
{
	public class FeatureFlagEntity
	{
		public string Key { get; set; } = string.Empty;
		public string ApplicationName { get; set; } = "global";
		public string ApplicationVersion { get; set; } = "0.0.0.0";
		public int Scope { get; set; }

		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;

		public string EvaluationModes { get; set; } = "[]";

		public DateTimeOffset? ScheduledEnableDate { get; set; }
		public DateTimeOffset? ScheduledDisableDate { get; set; }

		public TimeSpan? WindowStartTime { get; set; }
		public TimeSpan? WindowEndTime { get; set; }
		public string? TimeZone { get; set; }
		public string WindowDays { get; set; } = "[]";

		public string TargetingRules { get; set; } = "[]";

		public string EnabledUsers { get; set; } = "[]";
		public string DisabledUsers { get; set; } = "[]";
		public int UserPercentageEnabled { get; set; } = 100;

		public string EnabledTenants { get; set; } = "[]";
		public string DisabledTenants { get; set; } = "[]";
		public int TenantPercentageEnabled { get; set; } = 100;

		public string Variations { get; set; } = "{}";
		public string DefaultVariation { get; set; } = string.Empty;
	}
}
