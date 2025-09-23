using Propel.FeatureFlags.Dashboard.Api.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

public static class FeatureFlagExtensions
{
	public static void UpdateAuditTrail(this FeatureFlag flag, string username)
	{
		flag.Metadata.LastModified = new AuditTrail(timestamp: DateTime.UtcNow, actor: username);
	}
}
