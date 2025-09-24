using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public interface IDashboardRepository
{
	Task<FeatureFlag?> GetAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default);
	Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<PagedResult<FeatureFlag>> GetPagedAsync(int page, int pageSize, FeatureFlagFilter? filter = null, CancellationToken cancellationToken = default);
	Task<FeatureFlag> CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<FeatureFlag> UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<FeatureFlag> UpdateMetadataAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(FlagIdentifier identifier, string userid, string reason, CancellationToken cancellationToken = default);
}
