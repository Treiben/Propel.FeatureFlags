using Propel.FeatureFlags.Core;

namespace Propel.ClientApi.FeatureFlags;

// Type-safe feature flag definition for admin panel functionality.
// When this flag doesn't exist in the database, it will be automatically created
// with the specified default configuration to ensure zero-config deployments.
public class AdminPanelEnabledFeatureFlag : TypeSafeFeatureFlag
{
	public AdminPanelEnabledFeatureFlag()
		: base(key: "admin-panel-enabled",
			name: "Admin Panel Access", 
			description: "Controls access to administrative panel features including user management, system settings, and sensitive operations",
			applicationName: "Propel.ClientApi", 
			applicationVersion: "1.0.0", 
			tags: new()
				{
					{ "category", "security" },
					{ "impact", "high" },
					{ "team", "platform" },
					{ "environment", "all" }
				}, 
			isEnabledOnCreation: true)
	{
	}
}
