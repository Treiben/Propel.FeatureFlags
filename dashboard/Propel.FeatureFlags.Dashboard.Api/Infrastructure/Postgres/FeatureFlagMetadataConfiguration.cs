using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure.Entities;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.Postgres;

public class FeatureFlagMetadataConfiguration : IEntityTypeConfiguration<FeatureFlagMetadata>
{
	public void Configure(EntityTypeBuilder<FeatureFlagMetadata> builder)
	{
		// Table mapping
		builder.ToTable("feature_flags_metadata");

		// Primary key
		builder.HasKey(e => e.Id);

		// Column mappings
		builder.Property(e => e.Id)
			.HasColumnName("id")
			.HasDefaultValueSql("gen_random_uuid()");

		builder.Property(e => e.FlagKey)
			.HasColumnName("flag_key")
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

		builder.Property(e => e.IsPermanent)
			.HasColumnName("is_permanent")
			.HasDefaultValue(false)
			.IsRequired();

		builder.Property(e => e.ExpirationDate)
			.HasColumnName("expiration_date")
			.IsRequired();

		builder.Property(e => e.Tags)
			.HasColumnName("tags")
			.HasDefaultValue("{}")
			.IsRequired();

		builder.Property(e => e.Tags).HasColumnType("jsonb");
	}
}
