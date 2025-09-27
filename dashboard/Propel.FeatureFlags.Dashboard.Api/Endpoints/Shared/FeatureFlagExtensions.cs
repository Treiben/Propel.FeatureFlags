using Knara.UtcStrict;
using Propel.FeatureFlags.Dashboard.Api.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

public static class FeatureFlagExtensions
{
	public static void UpdateAuditTrail(this FeatureFlag flag, string action, string username)
	{
		flag.Metadata.LastModified = new AuditTrail(timestamp: UtcDateTime.UtcNow, actor: username, action: action);
	}
}
