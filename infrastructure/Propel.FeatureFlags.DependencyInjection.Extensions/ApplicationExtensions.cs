using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using System.Reflection;

namespace Propel.FeatureFlags.DependencyInjection.Extensions;

public static class ApplicationExtensions
{
	/// <summary>
	/// Automatically deploys all feature flags registered in the application.
	/// </summary>
	/// <remarks>This method retrieves all feature flags registered in the application and deploys them to the
	/// configured feature flag repository. If a feature flag factory is registered, it uses the factory to retrieve all
	/// flags and deploys each one individually. If no factory is registered, it attempts to deploy flags directly from the
	/// assembly. 
	/// <exception cref="InvalidOperationException">Thrown if the feature flag repository is not available. Ensure that the required services are properly configured.</exception>
	public static async Task AutoDeployFlags(this IHost host)
	{
		var repository = host.Services.GetRequiredService<IFeatureFlagRepository>()
			?? throw new InvalidOperationException("Feature flag repository is not available. " +
				"Make sure you added necessary flag services by calling ConfigureFeatureFlags() method from Propel.Extensions.DependencyInjection namespace.");

		// Get all registered feature flags from the factory
		var factory = host.Services.GetService<IFeatureFlagFactory>();
		if (factory is null)
		{
			await DeployFromAssemblyAsync(repository);
			return;
		}
		var allFlags = factory.GetAllFlags();
		foreach (var flag in allFlags)
		{
			await flag.DeployAsync(repository);
		}
	}

	private static async Task DeployFromAssemblyAsync(IFeatureFlagRepository repository)
	{
		var currentAssembly = Assembly.GetExecutingAssembly();
		var allFlags = currentAssembly
			.GetTypes()
			.Where(t => typeof(IFeatureFlag).IsAssignableFrom(t)
					&& !t.IsInterface
					&& !t.IsAbstract);
		foreach (var flag in allFlags)
		{
			var instance = (IFeatureFlag)Activator.CreateInstance(flag)!;
			await instance.DeployAsync(repository);
		}
	}
}
