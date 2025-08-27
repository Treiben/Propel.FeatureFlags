using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.AzureAppConfiguration;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddAzureAppConfigFeatureFlags(this IServiceCollection services, FlagOptions options)
	{
		services.AddSingleton<IFeatureFlagRepository>(sp =>
						new AzureAppConfigurationRepository(
							options.AzureAppConfigConnectionString ?? throw new InvalidOperationException("Azure App Configuration connection string required"),
							sp.GetRequiredService<ILogger<AzureAppConfigurationRepository>>()));

		return services;
	}
}
