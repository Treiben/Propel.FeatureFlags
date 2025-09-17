using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace Propel.FlagsManagement.Api.Endpoints.Shared;

public interface ICacheInvalidationService
{
	Task InvalidateFlagAsync(FlagKey flagKey, CancellationToken cancellationToken);
}

public sealed class CacheInvalidationService(IFeatureFlagCache? cache) : ICacheInvalidationService
{
	public async Task InvalidateFlagAsync(FlagKey flagKey, CancellationToken cancellationToken)
	{
		if (cache == null) return;

		CacheKey cacheKey = flagKey.Scope == Scope.Global
			? new GlobalCacheKey(flagKey.Key)
			: new ApplicationCacheKey(flagKey.Key, flagKey.ApplicationName!, flagKey.ApplicationVersion);

		await cache.RemoveAsync(cacheKey, cancellationToken);
	}
}
