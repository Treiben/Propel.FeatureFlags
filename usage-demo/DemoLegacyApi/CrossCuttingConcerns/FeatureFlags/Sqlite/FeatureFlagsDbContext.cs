using DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite
{
	public class FeatureFlagsDbContext : DbContext
	{
		public FeatureFlagsDbContext(DbContextOptions<FeatureFlagsDbContext> options)
			: base(options)
		{
		}

		public DbSet<FeatureFlagEntity> FeatureFlags { get; set; } = null!;
		public DbSet<FeatureFlagMetadataEntity> FeatureFlagsMetadata { get; set; } = null!;
		public DbSet<FeatureFlagAuditEntity> FeatureFlagsAudit { get; set; } = null!;

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// FeatureFlags entity configuration
			modelBuilder.Entity<FeatureFlagEntity>(entity =>
			{
				entity.ToTable("FeatureFlags");
				entity.HasKey(e => new { e.Key, e.ApplicationName, e.ApplicationVersion });

				entity.Property(e => e.Key).IsRequired().HasMaxLength(255);
				entity.Property(e => e.ApplicationName).IsRequired().HasMaxLength(255).HasDefaultValue("global");
				entity.Property(e => e.ApplicationVersion).IsRequired().HasMaxLength(100).HasDefaultValue("0.0.0.0");
				entity.Property(e => e.Scope).IsRequired().HasDefaultValue(0);

				entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
				entity.Property(e => e.Description).IsRequired().HasDefaultValue(string.Empty);

				entity.Property(e => e.EvaluationModes).IsRequired().HasDefaultValue("[]");
				entity.Property(e => e.WindowDays).IsRequired().HasDefaultValue("[]");
				entity.Property(e => e.TargetingRules).IsRequired().HasDefaultValue("[]");
				entity.Property(e => e.EnabledUsers).IsRequired().HasDefaultValue("[]");
				entity.Property(e => e.DisabledUsers).IsRequired().HasDefaultValue("[]");
				entity.Property(e => e.EnabledTenants).IsRequired().HasDefaultValue("[]");
				entity.Property(e => e.DisabledTenants).IsRequired().HasDefaultValue("[]");
				entity.Property(e => e.Variations).IsRequired().HasDefaultValue("{}");
				entity.Property(e => e.DefaultVariation).IsRequired().HasDefaultValue(string.Empty);

				entity.Property(e => e.UserPercentageEnabled).IsRequired().HasDefaultValue(100);
				entity.Property(e => e.TenantPercentageEnabled).IsRequired().HasDefaultValue(100);
			});

			// FeatureFlagsMetadata entity configuration
			modelBuilder.Entity<FeatureFlagMetadataEntity>(entity =>
			{
				entity.ToTable("FeatureFlagsMetadata");
				entity.HasKey(e => e.Id);

				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.Property(e => e.FlagKey).IsRequired().HasMaxLength(255);
				entity.Property(e => e.ApplicationName).IsRequired().HasMaxLength(255).HasDefaultValue("global");
				entity.Property(e => e.ApplicationVersion).IsRequired().HasMaxLength(100).HasDefaultValue("0.0.0.0");
				entity.Property(e => e.IsPermanent).IsRequired().HasDefaultValue(false);
				entity.Property(e => e.ExpirationDate).IsRequired();
				entity.Property(e => e.Tags).IsRequired().HasDefaultValue("{}");
			});

			// FeatureFlagsAudit entity configuration
			modelBuilder.Entity<FeatureFlagAuditEntity>(entity =>
			{
				entity.ToTable("FeatureFlagsAudit");
				entity.HasKey(e => e.Id);

				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.Property(e => e.FlagKey).IsRequired().HasMaxLength(255);
				entity.Property(e => e.ApplicationName).HasMaxLength(255).HasDefaultValue("global");
				entity.Property(e => e.ApplicationVersion).IsRequired().HasMaxLength(100).HasDefaultValue("0.0.0.0");
				entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
				entity.Property(e => e.Actor).IsRequired().HasMaxLength(255);
				entity.Property(e => e.Timestamp).IsRequired();
			});
		}
	}
}