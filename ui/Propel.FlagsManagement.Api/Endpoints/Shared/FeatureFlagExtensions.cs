using Propel.FeatureFlags.Domain;

namespace Propel.FlagsManagement.Api.Endpoints.Shared;

public static class FeatureFlagExtensions
{
	public static void UpdateAuditTrail(this FeatureFlag flag, string username)
	{
		flag.LastModified = new AuditTrail(timestamp: DateTime.UtcNow, actor: username);
	}
}
