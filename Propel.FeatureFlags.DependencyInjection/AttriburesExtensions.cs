using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Attributes;

namespace Propel.FeatureFlags.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
	public static IServiceCollection AddFeatureFlagsAttributes(this IServiceCollection services)
	{
		services.AddSingleton<IProxyGenerator, ProxyGenerator>();
		services.AddScoped<FeatureFlagInterceptor>();

		return services;
	}

	public static IServiceCollection RegisterService<TInterface, TImplementation>(this IServiceCollection services)
		where TInterface : class
		where TImplementation : class, TInterface
	{
		services.AddScoped(serviceProvider =>
		{
			var proxyGenerator = serviceProvider.GetRequiredService<IProxyGenerator>();
			var interceptor = serviceProvider.GetRequiredService<FeatureFlagInterceptor>();

			// Create the actual service instance
			var target = serviceProvider.GetRequiredService<TImplementation>();

			// Create proxy with interceptor
			var proxy = proxyGenerator.CreateInterfaceProxyWithTarget<TInterface>(
				target,
				interceptor);

			return proxy;
		});

		return services;
	}
}


