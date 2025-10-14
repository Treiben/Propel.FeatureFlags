using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Attributes.Interceptors;

namespace Propel.FeatureFlags.Attributes.Extensions;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds HTTP-based attribute interceptors and their dependencies to the specified service collection.
	/// </summary>
	public static IServiceCollection AddHttpAttributeInterceptors(this IServiceCollection services)
	{
		services.TryAddSingleton<IFeatureFlagEvaluator, HttpFeatureFlagEvaluator>();
		services.TryAddSingleton<IProxyGenerator, ProxyGenerator>();
		services.TryAddTransient<IInterceptor, FeatureFlagInterceptor>();

		return services;
	}

	/// <summary>
	/// Adds the necessary services for attribute-based method interception to the specified <see
	/// cref="IServiceCollection"/>.
	/// </summary>
	public static IServiceCollection AddAttributeInterceptors(this IServiceCollection services)
	{
		services.TryAddSingleton<IFeatureFlagEvaluator, DefaultEvaluator>();
		services.TryAddSingleton<IProxyGenerator, ProxyGenerator>();
		services.TryAddTransient<IInterceptor, FeatureFlagInterceptor>();

		return services;
	}

	/// <summary>
	/// Registers a service with feature flag interception, enabling dynamic behavior modification for the specified
	/// interface and implementation types.
	/// </summary>
	/// <remarks>This method registers the implementation type as a singleton and configures a scoped proxy for the
	/// interface type. The proxy uses an interceptor to enable feature flag-based behavior modification. Ensure that the
	/// required dependencies, such as <see cref="IProxyGenerator"/> and <see cref="IInterceptor"/>, are registered in the
	/// service collection.</remarks>
	/// <typeparam name="TInterface">The interface type that the service will expose.</typeparam>
	/// <typeparam name="TImplementation">The concrete implementation type of the interface.</typeparam>
	/// <param name="services">The <see cref="IServiceCollection"/> to which the service will be added.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	public static IServiceCollection RegisterWithFeatureFlagInterception<TInterface, TImplementation>(this IServiceCollection services)
	where TInterface : class
	where TImplementation : class, TInterface
	{
		services.TryAddSingleton<TImplementation>();

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


