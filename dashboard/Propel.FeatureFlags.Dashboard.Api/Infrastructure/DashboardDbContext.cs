using Microsoft.EntityFrameworkCore;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure.Entities;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public class DashboardDbContext(DbContextOptions<DashboardDbContext> options) : DbContext(options)
{
	public DbSet<FeatureFlag> FeatureFlags { get; set; } = null!;
	public DbSet<FeatureFlagMetadata> FeatureFlagMetadata { get; set; } = null!;
	public DbSet<FeatureFlagAudit> FeatureFlagAudit { get; set; } = null!;
}
