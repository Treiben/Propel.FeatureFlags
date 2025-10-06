using System;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite.Entities
{
	public class FeatureFlagMetadataEntity
	{
		public Guid Id { get; set; }
		public string FlagKey { get; set; } = string.Empty;
		public string ApplicationName { get; set; } = "global";
		public string ApplicationVersion { get; set; } = "0.0.0.0";
		public bool IsPermanent { get; set; }
		public DateTimeOffset ExpirationDate { get; set; }
		public string Tags { get; set; } = "{}";
	}
}
