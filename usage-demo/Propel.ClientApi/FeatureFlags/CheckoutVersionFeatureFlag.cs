using Propel.FeatureFlags.Core;

namespace Propel.ClientApi.FeatureFlags;


// Type-safe feature flag for controlling checkout processing variations.
// Enables A/B testing of different technical implementations while maintaining
// consistent business outcomes across all variations.
public class CheckoutVersionFeatureFlag : TypeSafeFeatureFlag
{
	public CheckoutVersionFeatureFlag() 
		: base(key: "checkout-version",
			name: "Checkout Processing Version",
			description: "Controls which checkout processing implementation is used for A/B testing. Supports v1 (legacy stable), v2 (enhanced with optimizations), and v3 (experimental cutting-edge algorithms). All variations achieve the same business outcome with different technical approaches.",
			tags: new()
				{
					{ "category", "performance" },
					{ "type", "a-b-test" },
					{ "impact", "medium" },
					{ "team", "checkout" },
					{ "variations", "v1,v2,v3" }
			})
	{
	}
}
