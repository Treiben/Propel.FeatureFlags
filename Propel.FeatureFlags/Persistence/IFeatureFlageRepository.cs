using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Persistence;

public interface IFeatureFlagRepository
{
	Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default);
	Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<List<FeatureFlag>> GetByTagsAsync(Dictionary<string, string> tags, CancellationToken cancellationToken = default);
	Task<FeatureFlag> CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<FeatureFlag> UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
	Task<List<FeatureFlag>> GetExpiringAsync(DateTime before, CancellationToken cancellationToken = default);
}
