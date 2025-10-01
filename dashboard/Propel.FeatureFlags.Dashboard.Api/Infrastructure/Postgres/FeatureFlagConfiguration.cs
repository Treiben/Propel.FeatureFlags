using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure.Entities;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Reflection.Emit;
using System.Text.Json;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.Postgres;

public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
	public void Configure(EntityTypeBuilder<FeatureFlag> builder)
	{
		// Table mapping with check constraints
		builder.ToTable("feature_flags", t =>
		{
			t.HasCheckConstraint("CK_FeatureFlag_UserPercentage",
				"user_percentage_enabled >= 0 AND user_percentage_enabled <= 100");
			t.HasCheckConstraint("CK_FeatureFlag_TenantPercentage",
				"tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100");
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
			.HasColumnName("key")
			.HasMaxLength(255)
			.IsRequired();

		builder.Property(e => e.ApplicationName)
			.HasColumnName("application_name")
			.HasMaxLength(255)
			.HasDefaultValue("global")
			.IsRequired();

		builder.Property(e => e.ApplicationVersion)
			.HasColumnName("application_version")
			.HasMaxLength(100)
			.HasDefaultValue("0.0.0.0")
			.IsRequired();

		builder.Property(e => e.Scope)
			.HasColumnName("scope")
			.HasDefaultValue(0)
			.IsRequired();

		builder.Property(e => e.Name)
			.HasColumnName("name")
			.HasMaxLength(500)
			.IsRequired();

		builder.Property(e => e.Description)
			.HasColumnName("description")
			.HasDefaultValue("")
			.IsRequired();

		// JSON columns - will be configured differently per provider
		builder.Property(e => e.EvaluationModes)
			.HasColumnName("evaluation_modes")
			.HasColumnType("jsonb")
			.HasDefaultValue("[]")
			.IsRequired();

		builder.Property(e => e.WindowDays)
			.HasColumnName("window_days")
			.HasDefaultValue("[]")
			.IsRequired();

		builder.Property(e => e.TargetingRules)
			.HasColumnName("targeting_rules")
			.HasDefaultValue("[]")
			.IsRequired();

		builder.Property(e => e.EnabledUsers)
			.HasColumnName("enabled_users")
			.HasDefaultValue("[]")
			.IsRequired();

		builder.Property(e => e.DisabledUsers)
			.HasColumnName("disabled_users")
			.HasDefaultValue("[]")
			.IsRequired();

		builder.Property(e => e.EnabledTenants)
			.HasColumnName("enabled_tenants")
			.HasDefaultValue("[]")
			.IsRequired();

		builder.Property(e => e.DisabledTenants)
			.HasColumnName("disabled_tenants")
			.HasDefaultValue("[]")
			.IsRequired();

		builder.Property(e => e.Variations)
			.HasColumnName("variations")
			.HasDefaultValue("{}")
			.IsRequired();

		// Scheduling columns
		builder.Property(e => e.ScheduledEnableDate)
			.HasColumnName("scheduled_enable_date");

		builder.Property(e => e.ScheduledDisableDate)
			.HasColumnName("scheduled_disable_date");

		// Time window columns
		builder.Property(e => e.WindowStartTime)
			.HasColumnName("window_start_time");

		builder.Property(e => e.WindowEndTime)
			.HasColumnName("window_end_time");

		builder.Property(e => e.TimeZone)
			.HasColumnName("time_zone")
			.HasMaxLength(100);

		// Percentage columns with check constraints
		builder.Property(e => e.UserPercentageEnabled)
			.HasColumnName("user_percentage_enabled")
			.HasDefaultValue(100)
			.IsRequired();

		builder.Property(e => e.TenantPercentageEnabled)
			.HasColumnName("tenant_percentage_enabled")
			.HasDefaultValue(100)
			.IsRequired();

		builder.Property(e => e.DefaultVariation)
			.HasColumnName("default_variation")
			.HasMaxLength(255)
			.HasDefaultValue("off")
			.IsRequired();

		// PostgreSQL - use JSONB
		builder.Property(e => e.EvaluationModes).HasColumnType("jsonb");
		builder.Property(e => e.WindowDays).HasColumnType("jsonb");
		builder.Property(e => e.TargetingRules).HasColumnType("jsonb");
		builder.Property(e => e.EnabledUsers).HasColumnType("jsonb");
		builder.Property(e => e.DisabledUsers).HasColumnType("jsonb");
		builder.Property(e => e.EnabledTenants).HasColumnType("jsonb");
		builder.Property(e => e.DisabledTenants).HasColumnType("jsonb");
		builder.Property(e => e.Variations).HasColumnType("jsonb");
	}
}
