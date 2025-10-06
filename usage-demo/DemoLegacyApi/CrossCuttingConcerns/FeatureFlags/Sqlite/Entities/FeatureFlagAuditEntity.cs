using System;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite.Entities
{
	public class FeatureFlagAuditEntity
	{
		public Guid Id { get; set; }
		public string FlagKey { get; set; } = string.Empty;
		public string ApplicationName { get; set; } = "global";
		public string ApplicationVersion { get; set; } = "0.0.0.0";
		public string Action { get; set; } = string.Empty;
		public string Actor { get; set; } = string.Empty;
		public DateTimeOffset Timestamp { get; set; }
		public string? Notes { get; set; }
	}
}
