using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.Extensions;

public static class FlagDeploymentExtensions
{
	//Ensure feature flags in database
	public static async Task DeployAsync(this IFeatureFlag flag,
		IFeatureFlagRepository repository,
		CancellationToken cancellationToken = default)
	{
		var flagIdentifier = new FlagIdentifier(flag.Key, Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
		var name = flag.Name ?? flag.Key;
		var description = flag.Description ?? $"Auto-deployed flag for {flag.Key} in application {ApplicationInfo.Name}";
		try
		{
			// Save to repository (if flag already exists, this will do nothing)
			await repository.CreateAsync(flagIdentifier, flag.OnOffMode, name, description, cancellationToken);
		}
		catch (Exception ex)
		{
			throw new Exception("Unable to deploy feature flag from the application. You can use a management tool option to create this flag in the database.");
		}
	}
}
