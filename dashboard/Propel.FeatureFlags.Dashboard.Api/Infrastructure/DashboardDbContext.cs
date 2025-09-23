using Microsoft.EntityFrameworkCore;
using Propel.FeatureFlags.Dashboard.Api.Entities;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public class DashboardDbContext(DbContextOptions<DashboardDbContext> options) : DbContext(options)
{
	public DbSet<FeatureFlag> FeatureFlags { get; set; } = null!;
	public DbSet<FeatureFlagMetadata> FeatureFlagMetadata { get; set; } = null!;
	public DbSet<FeatureFlagAudit> FeatureFlagAudit { get; set; } = null!;
}

public class PostgresDbContext(DbContextOptions<DashboardDbContext> options) : DashboardDbContext(options)
{
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new PostgresConfig.FeatureFlagConfiguration());
		modelBuilder.ApplyConfiguration(new PostgresConfig.FeatureFlagMetadataConfiguration());
		modelBuilder.ApplyConfiguration(new PostgresConfig.FeatureFlagAuditConfiguration());
	}
}

public class SqlServerDbContext(DbContextOptions<DashboardDbContext> options) : DashboardDbContext(options)
{
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new SqlServerConfig.FeatureFlagConfiguration());
		modelBuilder.ApplyConfiguration(new SqlServerConfig.FeatureFlagMetadataConfiguration());
		modelBuilder.ApplyConfiguration(new SqlServerConfig.FeatureFlagAuditConfiguration());
	}
}
