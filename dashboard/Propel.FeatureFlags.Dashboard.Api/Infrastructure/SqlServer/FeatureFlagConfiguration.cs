using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure.Entities;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.SqlServer;

public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        // Table mapping with check constraints
        builder.ToTable("FeatureFlags", t =>
        {
            t.HasCheckConstraint("CK_UserPercentage",
                "UserPercentageEnabled >= 0 AND UserPercentageEnabled <= 100");
            t.HasCheckConstraint("CK_TenantPercentage",
                "TenantPercentageEnabled >= 0 AND TenantPercentageEnabled <= 100");
            t.HasCheckConstraint("CK_EvaluationModes_json", "ISJSON(EvaluationModes) = 1");
            t.HasCheckConstraint("CK_WindowDays_json", "ISJSON(WindowDays) = 1");
            t.HasCheckConstraint("CK_TargetingRules_json", "ISJSON(TargetingRules) = 1");
            t.HasCheckConstraint("CK_EnabledUsers_json", "ISJSON(EnabledUsers) = 1");
            t.HasCheckConstraint("CK_DisabledUsers_json", "ISJSON(DisabledUsers) = 1");
            t.HasCheckConstraint("CK_EnabledTenants_json", "ISJSON(EnabledTenants) = 1");
            t.HasCheckConstraint("CK_DisabledTenants_json", "ISJSON(DisabledTenants) = 1");
            t.HasCheckConstraint("CK_Variations_json", "ISJSON(Variations) = 1");
        });

        // Composite primary key
        builder.HasKey(e => new { e.Key, e.ApplicationName, e.ApplicationVersion });

		//navigation properties - metadata
		builder.HasOne(e => e.Metadata)
			.WithOne(m => m.FeatureFlag)
			.HasForeignKey<FeatureFlagMetadata>(m => new { m.FlagKey, m.ApplicationName, m.ApplicationVersion });


		builder.HasMany(e => e.AuditTrail)
			.WithOne(a => a.FeatureFlag)
			.HasForeignKey(a => new { a.FlagKey, a.ApplicationName, a.ApplicationVersion });


		// Column mappings and constraints
		builder.Property(e => e.Key)
            .HasColumnName("Key")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.ApplicationName)
            .HasColumnName("ApplicationName")
            .HasMaxLength(255)
            .HasDefaultValue("global")
            .IsRequired();

        builder.Property(e => e.ApplicationVersion)
            .HasColumnName("ApplicationVersion")
            .HasMaxLength(100)
            .HasDefaultValue("0.0.0.0")
            .IsRequired();

        builder.Property(e => e.Scope)
            .HasColumnName("Scope")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("Name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("Description")
            .HasColumnType("NVARCHAR(MAX)")
            .HasDefaultValue("")
            .IsRequired();

        // JSON columns - SQL Server uses NVARCHAR(MAX)
        builder.Property(e => e.EvaluationModes)
            .HasColumnName("EvaluationModes")
            .HasColumnType("NVARCHAR(MAX)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(e => e.WindowDays)
            .HasColumnName("WindowDays")
            .HasColumnType("NVARCHAR(MAX)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(e => e.TargetingRules)
            .HasColumnName("TargetingRules")
            .HasColumnType("NVARCHAR(MAX)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(e => e.EnabledUsers)
            .HasColumnName("EnabledUsers")
            .HasColumnType("NVARCHAR(MAX)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(e => e.DisabledUsers)
            .HasColumnName("DisabledUsers")
            .HasColumnType("NVARCHAR(MAX)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(e => e.EnabledTenants)
            .HasColumnName("EnabledTenants")
            .HasColumnType("NVARCHAR(MAX)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(e => e.DisabledTenants)
            .HasColumnName("DisabledTenants")
            .HasColumnType("NVARCHAR(MAX)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(e => e.Variations)
            .HasColumnName("Variations")
            .HasColumnType("NVARCHAR(MAX)")
            .HasDefaultValue("{}")
            .IsRequired();

        // Scheduling columns
        builder.Property(e => e.ScheduledEnableDate)
            .HasColumnName("ScheduledEnableDate")
            .HasColumnType("DATETIMEOFFSET");

        builder.Property(e => e.ScheduledDisableDate)
            .HasColumnName("ScheduledDisableDate")
            .HasColumnType("DATETIMEOFFSET");

        // Time window columns
        builder.Property(e => e.WindowStartTime)
            .HasColumnName("WindowStartTime")
            .HasColumnType("TIME");

        builder.Property(e => e.WindowEndTime)
            .HasColumnName("WindowEndTime")
            .HasColumnType("TIME");

        builder.Property(e => e.TimeZone)
            .HasColumnName("TimeZone")
            .HasMaxLength(100);

        // Percentage columns
        builder.Property(e => e.UserPercentageEnabled)
            .HasColumnName("UserPercentageEnabled")
            .HasDefaultValue(100)
            .IsRequired();

        builder.Property(e => e.TenantPercentageEnabled)
            .HasColumnName("TenantPercentageEnabled")
            .HasDefaultValue(100)
            .IsRequired();

        builder.Property(e => e.DefaultVariation)
            .HasColumnName("DefaultVariation")
            .HasMaxLength(255)
            .HasDefaultValue("off")
            .IsRequired();
    }
}