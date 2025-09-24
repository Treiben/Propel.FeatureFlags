using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Attributes;
using Propel.FeatureFlags.Attributes.Interceptors;

namespace Propel.FeatureFlags.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddHttpAttributeInterceptors(this IServiceCollection services)
	{
		services.TryAddSingleton<IFeatureFlagEvaluator, HttpFeatureFlagEvaluator>();
		services.TryAddSingleton<IProxyGenerator, ProxyGenerator>();
		services.TryAddTransient<IInterceptor, FeatureFlagInterceptor>();

		return services;
	}

	public static IServiceCollection AddAttributeInterceptors(this IServiceCollection services)
	{
		services.TryAddSingleton<IFeatureFlagEvaluator, DefaultEvaluator>();
		services.TryAddSingleton<IProxyGenerator, ProxyGenerator>();
		services.TryAddTransient<IInterceptor, FeatureFlagInterceptor>();

		return services;
	}

	public static IServiceCollection RegisterServiceWithFlagAttributes<TInterface, TImplementation>(this IServiceCollection services)
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


