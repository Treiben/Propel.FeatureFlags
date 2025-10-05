using Propel.FeatureFlags.Domain;

namespace DemoWebApi.FeatureFlags;

// Type-safe feature flag definition for admin panel functionality.
// When this flag doesn't exist in the database, it will be automatically created
// with the specified default configuration to ensure zero-config deployments.

// If this flag does not exist in the database, it will be auto-created with these settings
// and will be enabled immediately.

// Note: It is often safer to default to disabled for high-impact features,
// so flag can be enabled on an approved schedule rather than immediately upon deployment.
public class AdminPanelEnabledFeatureFlag : FeatureFlagBase
{
	public AdminPanelEnabledFeatureFlag()
		: base(key: "admin-panel-enabled",
			name: "Admin Panel Access", 
			description: "Controls access to administrative panel features including user management, system settings, and sensitive operations",
			onOfMode: EvaluationMode.On)
	{
	}
}
