using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure.Entities;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.Postgres;

public class FeatureFlagAuditConfiguration : IEntityTypeConfiguration<FeatureFlagAudit>
{
	public void Configure(EntityTypeBuilder<FeatureFlagAudit> builder)
	{
		// Table mapping
		builder.ToTable("feature_flags_audit");

		// Primary key
		builder.HasKey(e => e.Id);

		// Column mappings
		builder.Property(e => e.Id)
			.HasColumnName("id")
			.HasDefaultValueSql("gen_random_uuid()"); // PostgreSQL function

		builder.Property(e => e.FlagKey)
			.HasColumnName("flag_key")
			.HasMaxLength(255)
			.IsRequired();

		builder.Property(e => e.ApplicationName)
			.HasColumnName("application_name")
			.HasMaxLength(255)
			.HasDefaultValue("global");

		builder.Property(e => e.ApplicationVersion)
			.HasColumnName("application_version")
			.HasMaxLength(100)
			.HasDefaultValue("0.0.0.0")
			.IsRequired();

		builder.Property(e => e.Action)
			.HasColumnName("action")
			.HasMaxLength(50)
			.IsRequired();

		builder.Property(e => e.Actor)
			.HasColumnName("actor")
			.HasMaxLength(255)
			.IsRequired();

		builder.Property(e => e.Timestamp)
			.HasColumnName("timestamp")
			.HasDefaultValueSql("NOW()") // PostgreSQL function
			.IsRequired();

		builder.Property(e => e.Notes)
			.HasColumnName("notes");
	}
}
