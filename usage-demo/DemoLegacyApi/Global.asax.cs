using DemoLegacyApi.CrossCuttingConcerns.FeatureFlags;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace DemoLegacyApi
{
	public class WebApiApplication : System.Web.HttpApplication
	{
		protected void Application_Start()
		{
			AreaRegistration.RegisterAllAreas();
			GlobalConfiguration.Configure(WebApiConfig.Register);
			FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
			RouteConfig.RegisterRoutes(RouteTable.Routes);
			BundleConfig.RegisterBundles(BundleTable.Bundles);

			// Bootstrap Feature Flags
			InitializeFeatureFlags();
		}

		private void InitializeFeatureFlags()
		{
			// Get the singleton container instance
			var container = FeatureFlagContainer.Instance;

			// Initialize the factory (this will scan and register all flags)
			var factory = container.GetOrCreateFlagFactory();

			// Get the repository from the container
			var repository = container.GetRepository();

			// Auto-deploy all flags to the repository
			// This will create flags in the persistence if they don't exist
			factory.AutoDeployFlags(repository).GetAwaiter().GetResult();

			System.Diagnostics.Debug.WriteLine("Feature flags initialized and deployed successfully");
		}

		// Note: feature flags for legacy applications require some boiler-plate code
		// to initialize the flag repository and deploy flags on application start.

		// FeatureFlagContainer is a static helper class to hold singleton instances
		// and help with initialization and flag deployment.
	}
}
