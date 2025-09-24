using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure.Entities;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.SqlServer;

public class FeatureFlagMetadataConfiguration : IEntityTypeConfiguration<FeatureFlagMetadata>
{
    public void Configure(EntityTypeBuilder<FeatureFlagMetadata> builder)
    {
        // Table mapping
        builder.ToTable("FeatureFlagsMetadata", t =>
        {
            t.HasCheckConstraint("CK_metadata_tags_json", "ISJSON(Tags) = 1");
        });

        // Primary key
        builder.HasKey(e => e.Id);

        // Column mappings
        builder.Property(e => e.Id)
            .HasColumnName("Id")
            .HasDefaultValueSql("NEWID()"); // SQL Server function

        builder.Property(e => e.FlagKey)
            .HasColumnName("FlagKey")
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

        builder.Property(e => e.IsPermanent)
            .HasColumnName("IsPermanent")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.ExpirationDate)
            .HasColumnName("ExpirationDate")
            .HasColumnType("DATETIMEOFFSET")
            .IsRequired();

        builder.Property(e => e.Tags)
            .HasColumnName("Tags")
            .HasColumnType("NVARCHAR(MAX)")
            .HasDefaultValue("{}")
            .IsRequired();
    }
}