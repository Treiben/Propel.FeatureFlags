using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

public interface ICacheInvalidationService
{
	Task InvalidateFlagAsync(FlagIdentifier identifier, CancellationToken cancellationToken);
}

public sealed class CacheInvalidationService(IFeatureFlagCache? cache = null) : ICacheInvalidationService
{
	public async Task InvalidateFlagAsync(FlagIdentifier identifier, CancellationToken cancellationToken)
	{
		if (cache == null) return;

		CacheKey cacheKey = identifier.Scope == Scope.Global
			? new GlobalCacheKey(identifier.Key)
			: new ApplicationCacheKey(identifier.Key, identifier.ApplicationName!, identifier.ApplicationVersion);

		await cache.RemoveAsync(cacheKey, cancellationToken);
	}
}
