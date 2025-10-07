using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.PostgreSql.Extensions;

internal static class DeploymentExtensions
{
	public static async Task DeployAsync(this IFeatureFlag flag,
		IFeatureFlagRepository repository,
		CancellationToken cancellationToken = default)
	{
		var name = flag.Name ?? flag.Key;
		var description = flag.Description ?? $"Auto-deployed flag for {flag.Key} in application {ApplicationInfo.Name}";
		// Save to repository (if flag already exists, this will do nothing)
		await repository.CreateApplicationFlagAsync(flag.Key, flag.OnOffMode, name, description, cancellationToken);
	}
}
