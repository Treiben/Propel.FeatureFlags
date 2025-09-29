using Microsoft.EntityFrameworkCore;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public interface IReadOnlyRepository
{
	Task<bool> FlagExistsAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default);
	Task<FeatureFlag?> GetAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default);
	Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<PagedResult<FeatureFlag>> GetPagedAsync(int page, int pageSize, FeatureFlagFilter? filter = null, CancellationToken cancellationToken = default);
}

public interface IDashboardRepository : IReadOnlyRepository
{
	Task<FeatureFlag> CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<FeatureFlag> UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<FeatureFlag> UpdateMetadataAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(FlagIdentifier identifier, string userid, string notes, CancellationToken cancellationToken = default);
}

public class BaseRepository(DashboardDbContext context) : IReadOnlyRepository
{
	public DashboardDbContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

	public async Task<FeatureFlag?> GetAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default)
	{
		var entity = await context.FeatureFlags
			.AsNoTracking()
			.Include(f => f.Metadata)
			.Include(f => f.AuditTrail)
			.FirstOrDefaultAsync(f =>
				f.Key == identifier.Key &&
				f.ApplicationName == (identifier.ApplicationName ?? "global") &&
				f.ApplicationVersion == (identifier.ApplicationVersion ?? "0.0.0.0") &&
				f.Scope == (int)identifier.Scope,
				cancellationToken);

		if (entity == null)
			return null;

		return Mapper.MapToDomain(entity);
	}

	public async Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		var entities = await context.FeatureFlags
						.AsNoTracking()
						.Include(f => f.Metadata)
						.Include(f => f.AuditTrail)
						.OrderBy(f => f.Name)
						.ThenBy(f => f.Key)
						.ToListAsync(cancellationToken);

		return [.. entities.Select(Mapper.MapToDomain)];
	}

	public async Task<PagedResult<FeatureFlag>> GetPagedAsync(int page, int pageSize, FeatureFlagFilter? filter = null, CancellationToken cancellationToken = default)
	{
		// Normalize page parameters
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var query = context.FeatureFlags.AsQueryable();

		// Apply filters
		if (filter != null)
		{
			query = Filtering.ApplyFilters(query, filter);
		}

		// Get total count before pagination
		var totalCount = await query.CountAsync(cancellationToken);

		// Apply pagination and ordering
		var entities = await query
			.AsNoTracking()
			.Include(f => f.Metadata)
			.Include(f => f.AuditTrail)
			.OrderBy(f => f.Name)
			.ThenBy(f => f.Key)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		return new PagedResult<FeatureFlag>
		{
			Items = [.. entities.Select(Mapper.MapToDomain)],
			TotalCount = totalCount,
			Page = page,
			PageSize = pageSize
		};
	}

	public async Task<bool> FlagExistsAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default)
	{
		var entity = await context.FeatureFlags
			.AsNoTracking()
			.FirstOrDefaultAsync(f =>
				f.Key == identifier.Key &&
				f.ApplicationName == (identifier.ApplicationName ?? "global") &&
				f.ApplicationVersion == (identifier.ApplicationVersion ?? "0.0.0.0") &&
				f.Scope == (int)identifier.Scope);
		return entity != null;
	}
}

public abstract class DashboardDbContext(DbContextOptions options) : DbContext(options)
{
	public DbSet<Entities.FeatureFlag> FeatureFlags { get; set; } = null!;
	public DbSet<Entities.FeatureFlagMetadata> FeatureFlagMetadata { get; set; } = null!;
	public DbSet<Entities.FeatureFlagAudit> FeatureFlagAudit { get; set; } = null!;
}
