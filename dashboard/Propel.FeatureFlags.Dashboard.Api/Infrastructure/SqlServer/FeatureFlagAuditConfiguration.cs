using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure.Entities;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.SqlServer;

public class FeatureFlagAuditConfiguration : IEntityTypeConfiguration<FeatureFlagAudit>
{
    public void Configure(EntityTypeBuilder<FeatureFlagAudit> builder)
    {
        // Table mapping
        builder.ToTable("FeatureFlagsAudit");

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
            .HasDefaultValue("global");

        builder.Property(e => e.ApplicationVersion)
            .HasColumnName("ApplicationVersion")
            .HasMaxLength(100)
            .HasDefaultValue("0.0.0.0")
            .IsRequired();

        builder.Property(e => e.Action)
            .HasColumnName("Action")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Actor)
            .HasColumnName("Actor")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Timestamp)
            .HasColumnName("Timestamp")
            .HasColumnType("DATETIMEOFFSET")
            .HasDefaultValueSql("GETUTCDATE()") // SQL Server function
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasColumnName("Notes")
            .HasColumnType("NVARCHAR(MAX)");
    }
}