using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Attributes.Extensions;
using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace Propel.FeatureFlags.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Configures feature flag services for the application, including optional caching and interception capabilities.
	/// </summary>
	/// <remarks>This method provides a flexible way to configure feature flags in an application. It supports the
	/// following features: <list type="bullet"> <item>Local caching of feature flags, if enabled in the
	/// configuration.</item> <item>Registration of feature flags with the dependency injection container.</item>
	/// <item>Support for attribute-based interception, including HTTP context-based interceptors.</item> <item>Optional
	/// registration of a feature flag factory for dynamic flag creation.</item> </list> 
	/// Ensure that the <paramref name="configure"/> action, if provided,  properly
	/// initializes the <see cref="PropelConfiguration"/> object to match the application's requirements.</remarks>
	/// <param name="services">The <see cref="IServiceCollection"/> to which the feature flag services will be added.</param>
	/// <param name="configure">An optional action to configure additional settings for feature flags using a <see cref="PropelConfiguration"/>
	/// object.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance with the configured feature flag services.</returns>
	public static IServiceCollection ConfigureFeatureFlags(this IServiceCollection services, Action<PropelConfiguration>? configure)
	{
		var config = new PropelConfiguration();
		configure.Invoke(config);

		services.AddFeatureFlagServices(config);

		if (config.LocalCacheConfiguration.LocalCacheEnabled)
		{
			services.AddLocalCache(config.LocalCacheConfiguration);
		}

		if (config.RegisterFlagsWithContainer)
		{
			services.RegisterFlagsFromExecutingAssembly();
		}

		var aopOptions = config.Interception;
		if (aopOptions.EnableIntercepter)
		{
			// Register the interceptor service
			services.AddAttributeInterceptors();
		}

		if (aopOptions.EnableHttpIntercepter)
		{
			// Register the HTTP interceptor service
			services.AddHttpContextAccessor(); // required for HttpContext-based attributes
			services.AddHttpAttributeInterceptors();
		}

		if (config.EnableFlagFactory)
			services.TryAddSingleton<IFeatureFlagFactory, FeatureFlagFactory>();

		return services;
	}

	internal static IServiceCollection AddLocalCache(this IServiceCollection services, LocalCacheConfiguration cacheConfiguration)
	{
		// Add memory cache for local caching layer
		services.AddMemoryCache(memOptions =>
		{
			memOptions.SizeLimit = cacheConfiguration.CacheSizeLimit;
			memOptions.CompactionPercentage = 0.25; // Compact 25% when limit reached
		});
		services.TryAddSingleton(cacheConfiguration);
		services.TryAddSingleton<IFeatureFlagCache, LocalFeatureFlagCache>();
		return services;
	}
}
