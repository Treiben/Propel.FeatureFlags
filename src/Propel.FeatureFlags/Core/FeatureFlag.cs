namespace Propel.FeatureFlags.Core
{
	public class FeatureFlag
	{
		public string Key { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public FeatureFlagStatus Status { get; set; } = FeatureFlagStatus.Disabled;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
		public string CreatedBy { get; set; } = string.Empty;
		public string UpdatedBy { get; set; } = string.Empty;
		public DateTime? ExpirationDate { get; set; }

		// Scheduling
		public DateTime? ScheduledEnableDate { get; set; }
		public DateTime? ScheduledDisableDate { get; set; }

		// Time Window
		public TimeSpan? WindowStartTime { get; set; }
		public TimeSpan? WindowEndTime { get; set; }
		public string? TimeZone { get; set; }
		public List<DayOfWeek>? WindowDays { get; set; }

		// Percentage rollout
		public int PercentageEnabled { get; set; } = 0;

		// Targeting
		public List<TargetingRule> TargetingRules { get; set; } = new List<TargetingRule>();
		public List<string> EnabledUsers { get; set; } = new List<string>();
		public List<string> DisabledUsers { get; set; } = new List<string>();

		// Tenant-level controls
		public List<string> EnabledTenants { get; set; } = new List<string>();
		public List<string> DisabledTenants { get; set; } = new List<string>();
		public int TenantPercentageEnabled { get; set; } = 0;

		// Variations for A/B testing
		public Dictionary<string, object> Variations { get; set; } = new Dictionary<string, object>();
		public string DefaultVariation { get; set; } = "off";

		// Metadata
		public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
		public bool IsPermanent { get; set; } = false;
	}
}
