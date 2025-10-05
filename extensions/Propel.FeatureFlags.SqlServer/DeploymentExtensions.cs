using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.SqlServer;

internal static class DeploymentExtensions
{
	//Ensure feature flags in database
	public static async Task DeployAsync(this IFeatureFlag flag,
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
