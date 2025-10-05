using Propel.FeatureFlags.Domain;

namespace DemoWebApi.FeatureFlags;


// Type-safe feature flag for controlling checkout processing variations.
// Enables A/B testing of different technical implementations while maintaining
// consistent business outcomes across all variations.

// The flag created with default settings if it does not already exist in the database
// in disabled state (Recommended).

// Note: It is often safer to default to disabled so when the feature is deployed it can be enabled
// on approved release schedule rather than immeditately,
public class CheckoutVersionFeatureFlag : FeatureFlagBase
{
	public CheckoutVersionFeatureFlag() 
		: base(key: "checkout-version",
			name: "Checkout Processing Version",
			description: "Controls which checkout processing implementation is used for A/B testing. Supports v1 (legacy stable), v2 (enhanced with optimizations), and v3 (experimental cutting-edge algorithms). All variations achieve the same business outcome with different technical approaches.")
	{
	}
}
