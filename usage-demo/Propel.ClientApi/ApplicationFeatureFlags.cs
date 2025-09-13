using Propel.ClientApi.FeatureFlags;

namespace Propel.ClientApi;

public class ApplicationFeatureFlags
{
	public static readonly AdminPanelEnabledFeatureFlag AdminPanelEnabledFeatureFlag = new();

	public static readonly CheckoutVersionFeatureFlag CheckoutVersionFeatureFlag = new();

	public static readonly NewProductApiFeatureFlag NewProductApiFeatureFlag = new();

	public static readonly FeaturedProductsLaunchFeatureFlag FeaturedProductsLaunchFeatureFlag = new();

	public static readonly EnhancedCatalogUiFeatureFlag EnhancedCatalogUiFeatureFlag = new();

	public static readonly NewPaymentProcessorFeatureFlag NewPaymentProcessorFeatureFlag = new();

	public static readonly RecommendationAlgorithmFeatureFlag RecommendationAlgorithmFeatureFlag = new();
}
