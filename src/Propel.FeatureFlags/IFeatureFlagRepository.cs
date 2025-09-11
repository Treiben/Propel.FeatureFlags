using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags;

public record FeatureFlagFilter
{
	public Dictionary<string, string>? Tags { get; set; }
	public EvaluationMode[]? EvaluationModes { get; set; }
	public int? ExpiringInDays { get; set; }
}

public record PagedResult<T>
{
	public List<T> Items { get; set; } = [];
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
	public bool HasNextPage => Page < TotalPages;
	public bool HasPreviousPage => Page > 1;
}

public interface IFeatureFlagRepository
{
	Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default);
	Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<PagedResult<FeatureFlag>> GetPagedAsync(int page, int pageSize, FeatureFlagFilter? filter = null, CancellationToken cancellationToken = default);
	Task<FeatureFlag> CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<FeatureFlag> UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
