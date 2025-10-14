using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.Cache;

/// <summary>
/// Defines a contract for a cache that stores and retrieves feature flag evaluation options.
/// </summary>
/// <remarks>This interface provides methods to asynchronously manage feature flag data in a cache,  including
/// retrieving, storing, and removing evaluation options. It is designed to support  scenarios where feature flag
/// evaluations need to be cached for performance or consistency.</remarks>
public interface IFeatureFlagCache
{
	Task<EvaluationOptions?> GetAsync(CacheKey key, CancellationToken cancellationToken = default);
	Task SetAsync(CacheKey key, EvaluationOptions flag, CancellationToken cancellationToken = default);
	Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);
}
