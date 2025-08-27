using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Propel.FeatureFlags.Attributes;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddHttpFeatureFlagsAttributes(this IServiceCollection services)
	{
		services.TryAddSingleton<IProxyGenerator, ProxyGenerator>();
		services.TryAddTransient<IInterceptor, HttpFeatureFlagInterceptor>();

		return services;
	}

	public static IServiceCollection AddFeatureFlagsAttributes(this IServiceCollection services)
	{
		services.TryAddSingleton<IProxyGenerator, ProxyGenerator>();
		services.TryAddTransient<IInterceptor, FeatureFlagInterceptor>();

		return services;
	}

	public static IServiceCollection AddScopedWithFeatureFlags<TInterface, TImplementation>(this IServiceCollection services)
	where TInterface : class
	where TImplementation : class, TInterface
	{
		services.AddSingleton<TImplementation>();

		services.TryAddScoped(provider =>
		{
			var proxyGenerator = provider.GetRequiredService<IProxyGenerator>();
			var target = provider.GetRequiredService<TImplementation>();
			var interceptor = provider.GetRequiredService<IInterceptor>();
			return proxyGenerator.CreateInterfaceProxyWithTarget<TInterface>(target, interceptor);
		});

		return services;
	}
}


