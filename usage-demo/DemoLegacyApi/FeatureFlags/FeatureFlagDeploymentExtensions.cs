using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DemoLegacyApi.FeatureFlags
{
	public static class FeatureFlagDeploymentExtensions
	{
		public static async Task AutoDeployFlags(this IFeatureFlagRepository repository)
		{
			if (repository is null)
				throw new InvalidOperationException("Feature flag repository is not available. " +
					"Make sure you added necessary flag services by calling ConfigureFeatureFlags() method from Propel.CoreExtensions.PostgreSql namespace.");

			// Get all registered feature flags from the factory
			await DeployFromAssemblyAsync(repository);
		}

		public static async Task AutoDeployFlags(this IFeatureFlagFactory factory, IFeatureFlagRepository repository)
		{
			if (factory is null)
				throw new ArgumentNullException(nameof(factory));
			if (repository is null)
				throw new ArgumentNullException(nameof(repository));
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

		private static async Task DeployAsync(this IFeatureFlag flag,
				IFeatureFlagRepository repository,
				CancellationToken cancellationToken = default)
		{
			var flagIdentifier = new FlagIdentifier(flag.Key, Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
			var name = flag.Name ?? flag.Key;
			var description = flag.Description ?? $"Auto-deployed flag for {flag.Key} in application {ApplicationInfo.Name}";
			// Save to repository (if flag already exists, this will do nothing)
			await repository.CreateApplicationFlagAsync(flagIdentifier, flag.OnOffMode, name, description, cancellationToken);
		}
	}
}