using Microsoft.EntityFrameworkCore;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Domain;
using System.Runtime.CompilerServices;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public interface IReadOnlyRepository
{
	Task<bool> FlagExistsAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default);
	Task<FeatureFlag?> GetByKeyAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default);
	Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<List<FeatureFlag>> FindAsync(FindFlagCriteria criteria, CancellationToken cancellationToken = default);
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

	public async Task<FeatureFlag?> GetByKeyAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default)
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

	public async Task<PagedResult<FeatureFlag>> GetPagedAsync(int page, int pageSize, 
						FeatureFlagFilter? filter = null, CancellationToken cancellationToken = default)
	{
		// Normalize page parameters
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var provider = context.Database.ProviderName ?? string.Empty;

		// Determine which filtering to use based on database provider
		string sql;
		string countSql;
		Dictionary<string, object> parameters;

		if (provider.Contains("Npgsql") || provider.Contains("PostgreSQL"))
		{
			// Use PostgreSQL filtering
			sql = PostgresFiltering.BuildFilterQuery(page, pageSize, filter!);
			countSql = PostgresFiltering.BuildCountQuery(filter!);
			(_, parameters) = PostgresFiltering.BuildFilterConditions(filter!);
		}
		else if (provider.Contains("SqlServer"))
		{
			// Use SQL Server filtering
			sql = SqlServerFiltering.BuildFilterQuery(page, pageSize, filter!);
			countSql = SqlServerFiltering.BuildCountQuery(filter!);
			(_, parameters) = SqlServerFiltering.BuildFilterConditions(filter);
		}
		else
		{
			throw new NotSupportedException($"Database provider '{provider}' is not supported for filtering operations.");
		}

		// Execute queries
		var entities = await ExecutePagedQuery(sql, parameters, cancellationToken);
		var totalCount = await ExecuteCountQuery(countSql, parameters, cancellationToken);

		return new PagedResult<FeatureFlag>
		{
			Items = [.. entities.Select(Mapper.MapToDomain)],
			TotalCount = totalCount,
			Page = page,
			PageSize = pageSize
		};
	}

	public async Task<List<FeatureFlag>> FindAsync(FindFlagCriteria criteria, CancellationToken cancellationToken = default)
	{
		var entities = await context.FeatureFlags
			.AsNoTracking()
			.Include(f => f.Metadata)
			.Include(f => f.AuditTrail)
			.Where(f =>
				(!string.IsNullOrWhiteSpace(criteria.Key) && f.Key.ToLower().Equals(criteria.Key.ToLower())) ||
				(!string.IsNullOrWhiteSpace(criteria.Name) && f.Name.ToLower().Contains(criteria.Name.ToLower())) ||
				(!string.IsNullOrWhiteSpace(criteria.Description) && f.Description.ToLower().Contains(criteria.Description.ToLower())))
			.ToListAsync(cancellationToken);

		if (entities == null)
			return [];

		return [.. entities.Select(Mapper.MapToDomain)];
	}

	public async Task<bool> FlagExistsAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default)
	{
		var entity = await context.FeatureFlags
			.AsNoTracking()
			.FirstOrDefaultAsync(f =>
				f.Key == identifier.Key &&
				f.ApplicationName == (identifier.ApplicationName ?? "global") &&
				f.ApplicationVersion == (identifier.ApplicationVersion ?? "0.0.0.0") &&
				f.Scope == (int)identifier.Scope, cancellationToken);
		return entity != null;
	}

	private async Task<List<Entities.FeatureFlag>> ExecutePagedQuery(string sql, Dictionary<string, object> parameters, CancellationToken cancellationToken)
	{
		// Create FormattableString for FromSqlInterpolated
		var formattableString = CreateFormattableString(sql, parameters);

		return await context.FeatureFlags
			.FromSqlInterpolated(formattableString)
			.AsNoTracking()
			.Include(f => f.Metadata)
			.Include(f => f.AuditTrail)
			.ToListAsync(cancellationToken);
	}

	private async Task<int> ExecuteCountQuery(string sql, Dictionary<string, object> parameters, CancellationToken cancellationToken)
	{
		var formattableString = CreateFormattableString(sql, parameters);

		// For count queries, we need to execute and get the scalar result differently
		var connection = context.Database.GetDbConnection();

		await using var command = connection.CreateCommand();
		command.CommandText = formattableString.Format;

		// Add parameters properly
		command.CommandText = string.Format(command.CommandText,
			[.. formattableString.GetArguments().Select(i => $"'{i ?? DBNull.Value}'")]);

		if (connection.State != System.Data.ConnectionState.Open)
			await connection.OpenAsync(cancellationToken);

		var scalarResult = await command.ExecuteScalarAsync(cancellationToken);
		return Convert.ToInt32(scalarResult ?? 0);
	}

	private FormattableString CreateFormattableString(string sql, Dictionary<string, object> parameters)
	{
		var args = parameters.Values.ToArray();
		var parameterizedSql = sql;

		// Replace parameter placeholders with {0}, {1}, etc.
		int index = 0;
		foreach (var key in parameters.Keys)
		{
			// Handle both @paramName (SQL Server) and {paramName} (PostgreSQL) formats
			parameterizedSql = parameterizedSql.Replace($"@{key}", $"{{{index}}}");
			parameterizedSql = parameterizedSql.Replace($"{{{key}}}", $"{{{index}}}");
			index++;
		}

		return FormattableStringFactory.Create(parameterizedSql, args);
	}
}

public abstract class DashboardDbContext(DbContextOptions options) : DbContext(options)
{
	public DbSet<Entities.FeatureFlag> FeatureFlags { get; set; } = null!;
	public DbSet<Entities.FeatureFlagMetadata> FeatureFlagMetadata { get; set; } = null!;
	public DbSet<Entities.FeatureFlagAudit> FeatureFlagAudit { get; set; } = null!;
}
