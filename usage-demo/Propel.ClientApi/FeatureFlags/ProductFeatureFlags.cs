using Propel.FeatureFlags.Domain;

namespace ApiFlagUsageDemo.FeatureFlags;

// Type-safe feature flag for controlling the new product API implementation.
// Enables A/B testing between legacy and enhanced product API endpoints
// with improved performance and additional product data.
public class NewProductApiFeatureFlag : FeatureFlagBase
{
	public NewProductApiFeatureFlag() 
		: base(key: "new-product-api", 
			name: "New Product API", 
			description: "Controls whether to use the new enhanced product API implementation with improved performance and additional product data, or fall back to the legacy API. Enables safe rollout of API improvements without affecting existing functionality.")
	{
	}
}

// Type-safe feature flag for controlling the scheduled launch of featured products.
// Enables coordinated release of new featured product displays and promotions
// at a specific date and time across the platform.
public class FeaturedProductsLaunchFeatureFlag : FeatureFlagBase
{
	public FeaturedProductsLaunchFeatureFlag() 
		: base(key: "featured-products-launch",
			name: "Featured Products Launch", 
			description: "Controls the scheduled launch of enhanced featured products display with new promotions and special pricing. Designed for coordinated marketing campaigns and product launches that require precise timing across all platform touchpoints.")
	{
	}
}

// Type-safe feature flag for controlling enhanced catalog UI features.
// Enables advanced catalog functionality during business hours when
// customer support is available to assist with the more complex interface.
public class EnhancedCatalogUiFeatureFlag : FeatureFlagBase
{
	public EnhancedCatalogUiFeatureFlag()
		: base(key: "enhanced-catalog-ui",
			name: "Enhanced Catalog UI", 
			description: "Controls whether to display the enhanced catalog interface with advanced features like detailed analytics, live chat, and smart recommendations. Typically enabled during business hours when customer support is available to assist users with the more complex interface features.",
			onOfMode: EvaluationMode.Off)
	{
	}
}