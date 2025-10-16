using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.DependencyInjection.Extensions;

internal static class FeatureFlagExtensions
{
	internal static async Task DeployAsync(this IFeatureFlag flag,
		IFeatureFlagRepository repository,
		CancellationToken cancellationToken = default)
	{
		var name = flag.Name ?? flag.Key;
		var description = flag.Description ?? $"Auto-deployed flag for {flag.Key} in application {ApplicationInfo.Name}";
		var identifier = new ApplicationFlagIdentifier(flag.Key);
		// Save to repository (if flag already exists, this will do nothing)
		await repository.CreateApplicationFlagAsync(identifier, flag.OnOffMode, name, description, cancellationToken);
	}
}
