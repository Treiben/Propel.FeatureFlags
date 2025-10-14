using DemoLegacyApi.CrossCuttingConcerns.FeatureFlags;
using DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite;
using System;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace DemoLegacyApi
{
	public class WebApiApplication : System.Web.HttpApplication
	{
		public const string InMemoryConnectionString = "Data Source=InMemoryFeatureFlags;Mode=Memory;Cache=Shared";
		
		protected void Application_Start()
		{
			AreaRegistration.RegisterAllAreas();
			GlobalConfiguration.Configure(WebApiConfig.Register);
			FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
			RouteConfig.RegisterRoutes(RouteTable.Routes);
			BundleConfig.RegisterBundles(BundleTable.Bundles);

			Environment.SetEnvironmentVariable("APP_NAME", "DemoLegacyApi");
			Environment.SetEnvironmentVariable("APP_VERSION", "1.0.0");

			// Bootstrap Feature Flags
			InitializeFeatureFlags();
		}

		protected void Application_End()
		{
			// Close the persistent in-memory database connection
			FeatureFlagContainer.CloseDatabase();
		}

		// Note: feature flags for legacy applications require some boiler-plate code
		// to initialize the flag repository and deploy flags on application start.
		private void InitializeFeatureFlags()
		{
			// Get the singleton container instance (this opens the persistent connection)
			var container = FeatureFlagContainer.Instance;

			// Initialize cache
			

			// Initialize database FIRST (this must happen before creating the container)
			InitializeDatabaseAsync().GetAwaiter().GetResult();

			// Initialize the factory (this will scan and register all flags)
			var factory = container.GetOrCreateFlagFactory();

			// Get the repository from the container
			var repository = container.GetRepository();

			// Auto-deploy all flags to the repository
			// This will create flags in the persistence if they don't exist
			factory.AutoDeployFlags(repository).GetAwaiter().GetResult();

			System.Diagnostics.Debug.WriteLine("Feature flags initialized and deployed successfully");
		}

		private async Task InitializeDatabaseAsync()
		{
			try
			{
				var initializer = FeatureFlagContainer.Instance.GetDatabaseInitializer();

				var success = await initializer.InitializeAsync();
				if (!success)
				{
					throw new InvalidOperationException("Failed to initialize SQLite in-memory database");
				}
				Console.WriteLine("SQLite in-memory database initialized successfully");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error initializing SQLite database: {ex.Message}");
				throw;
			}
		}
	}
}
