using System.Reflection;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

public interface IEndpoint
{
	void AddEndpoint(IEndpointRouteBuilder epRoutBuilder);
}

public static class IEndpointExtensions
{
	public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder epRouteBuilder)
	{
		foreach (var ep in epRouteBuilder.ServiceProvider.GetServices<IEndpoint>())
		{
			ep.AddEndpoint(epRouteBuilder);
		}

		return epRouteBuilder;
	}

	public static IServiceCollection RegisterDashboardEndpoints(this IServiceCollection services)
	{
		var currentAssembly = Assembly.GetExecutingAssembly();

		var epTypes = currentAssembly
			.GetTypes()
			.Where(t => typeof(IEndpoint).IsAssignableFrom(t)
					&& !t.IsInterface
					&& !t.IsAbstract);

		foreach (var ep in epTypes)
		{
			var sliceInstance = (IEndpoint)Activator.CreateInstance(ep)!;
			services.AddSingleton(typeof(IEndpoint), ep);
		}

		return services;
	}
}
