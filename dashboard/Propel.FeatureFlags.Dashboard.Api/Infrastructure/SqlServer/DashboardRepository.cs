using Microsoft.EntityFrameworkCore;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Domain;
using System.Text.Json;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.SqlServer;

public class SqlServerDbContext(DbContextOptions<SqlServerDbContext> options) : DashboardDbContext(options)
{
	protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new FeatureFlagConfiguration());
        modelBuilder.ApplyConfiguration(new FeatureFlagMetadataConfiguration());
        modelBuilder.ApplyConfiguration(new FeatureFlagAuditConfiguration());
    }
}

public class DashboardRepository(SqlServerDbContext context) : BaseRepository(context), IDashboardRepository
{
	public async Task<FeatureFlag> CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
    {
        var identifier = flag.Identifier;
        var metadata = flag.Metadata;
        var config = flag.Configuration;

        await context.Database.ExecuteSqlRawAsync(@"
        INSERT INTO FeatureFlags (
            [Key], ApplicationName, ApplicationVersion, Scope, [Name], [Description],
            EvaluationModes, ScheduledEnableDate, ScheduledDisableDate,
            WindowStartTime, WindowEndTime, TimeZone, WindowDays,
            TargetingRules, EnabledUsers, DisabledUsers, UserPercentageEnabled,
            EnabledTenants, DisabledTenants, TenantPercentageEnabled,
            Variations, DefaultVariation
        ) VALUES (
            {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12},
            {13}, {14}, {15}, {16}, {17}, {18}, {19}, {20}, {21}
        );

        INSERT INTO FeatureFlagsMetadata (
            FlagKey, ApplicationName, ApplicationVersion,
            IsPermanent, ExpirationDate, Tags
        ) VALUES ({0}, {1}, {2}, {22}, {23}, {24});

        INSERT INTO FeatureFlagsAudit (
            FlagKey, ApplicationName, ApplicationVersion,
            [Action], Actor, Reason, [Timestamp]
        ) VALUES ({0}, {1}, {2}, 'flag-created', {25}, {26}, {27});",
        identifier.Key,
        identifier.ApplicationName ?? "global",
        identifier.ApplicationVersion ?? "0.0.0.0",
        (int)identifier.Scope,
        metadata.Name,
        metadata.Description,
        JsonSerializer.Serialize(config.ActiveEvaluationModes.Modes.Select(m => (int)m).ToArray()),
        config.Schedule.HasSchedule() ? (DateTimeOffset)config.Schedule.EnableOn : null!,
        config.Schedule.HasSchedule() ? (DateTimeOffset)config.Schedule.DisableOn : null!,
        config.OperationalWindow.HasWindow() ? config.OperationalWindow.StartOn : null!,
        config.OperationalWindow.HasWindow() ? config.OperationalWindow.StopOn : null!,
        config.OperationalWindow.HasWindow() ? config.OperationalWindow.TimeZone : null!,
        JsonSerializer.Serialize(config.OperationalWindow.DaysActive.Select(d => (int)d).ToArray()),
        JsonSerializer.Serialize(config.TargetingRules),
        JsonSerializer.Serialize(config.UserAccessControl.Allowed),
        JsonSerializer.Serialize(config.UserAccessControl.Blocked),
        config.UserAccessControl.RolloutPercentage,
        JsonSerializer.Serialize(config.TenantAccessControl.Allowed),
        JsonSerializer.Serialize(config.TenantAccessControl.Blocked),
        config.TenantAccessControl.RolloutPercentage,
        JsonSerializer.Serialize(config.Variations.Values),
        config.Variations.DefaultVariation,
        metadata.RetentionPolicy.IsPermanent,
        (DateTimeOffset)metadata.RetentionPolicy.ExpirationDate,
        JsonSerializer.Serialize(metadata.Tags),
        metadata.Created.Actor ?? "anonymous",
        metadata.Created.Reason ?? "Flag created from the website",
        (DateTimeOffset)metadata.Created.Timestamp);

        return flag;
    }

    public async Task<FeatureFlag> UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
    {
        var identifier = flag.Identifier;
        var metadata = flag.Metadata;
        var config = flag.Configuration;

        var updatedRows = await context.Database.ExecuteSqlRawAsync(@"
        UPDATE FeatureFlags SET
            [Name] = {4}, [Description] = {5}, EvaluationModes = {6},
            ScheduledEnableDate = {7}, ScheduledDisableDate = {8},
            WindowStartTime = {9}, WindowEndTime = {10}, TimeZone = {11},
            WindowDays = {12}, TargetingRules = {13},
            EnabledUsers = {14}, DisabledUsers = {15}, UserPercentageEnabled = {16},
            EnabledTenants = {17}, DisabledTenants = {18}, TenantPercentageEnabled = {19},
            Variations = {20}, DefaultVariation = {21}
        WHERE [Key] = {0} AND ApplicationName = {1} AND ApplicationVersion = {2} AND Scope = {3};

        INSERT INTO FeatureFlagsAudit (
            FlagKey, ApplicationName, ApplicationVersion,
            [Action], Actor, Reason, [Timestamp]
        ) VALUES ({0}, {1}, {2}, 'flag-modified', {22}, {23}, {24});",
        identifier.Key,
        identifier.ApplicationName ?? "global",
        identifier.ApplicationVersion ?? "0.0.0.0",
        (int)identifier.Scope,
        metadata.Name,
        metadata.Description,
        JsonSerializer.Serialize(config.ActiveEvaluationModes.Modes.Select(m => (int)m).ToArray()),
        config.Schedule.HasSchedule() ? (DateTimeOffset)config.Schedule.EnableOn : null!,
        config.Schedule.HasSchedule() ? (DateTimeOffset)config.Schedule.DisableOn : null!,
        config.OperationalWindow.HasWindow() ? config.OperationalWindow.StartOn : null!,
        config.OperationalWindow.HasWindow() ? config.OperationalWindow.StopOn : null!,
        config.OperationalWindow.HasWindow() ? config.OperationalWindow.TimeZone : null!,
        JsonSerializer.Serialize(config.OperationalWindow.DaysActive.Select(d => (int)d).ToArray()),
        JsonSerializer.Serialize(config.TargetingRules),
        JsonSerializer.Serialize(config.UserAccessControl.Allowed),
        JsonSerializer.Serialize(config.UserAccessControl.Blocked),
        config.UserAccessControl.RolloutPercentage,
        JsonSerializer.Serialize(config.TenantAccessControl.Allowed),
        JsonSerializer.Serialize(config.TenantAccessControl.Blocked),
        config.TenantAccessControl.RolloutPercentage,
        JsonSerializer.Serialize(config.Variations.Values),
        config.Variations.DefaultVariation,
        metadata.LastModified?.Actor ?? "anonymous",
        metadata.LastModified?.Reason ?? "Flag updated",
		(DateTimeOffset)(metadata.LastModified?.Timestamp ?? DateTimeOffset.UtcNow));

        if (updatedRows == 0)
        {
            throw new InvalidOperationException($"Feature flag with key '{identifier.Key}' not found");
        }

        return flag;
    }

    public async Task<FeatureFlag> UpdateMetadataAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
    {
        var identifier = flag.Identifier;
        var metadata = flag.Metadata;

        await context.Database.ExecuteSqlRawAsync(@"
        MERGE FeatureFlagsMetadata AS target
        USING (SELECT {0} AS FlagKey, {1} AS ApplicationName, {2} AS ApplicationVersion) AS source
        ON target.FlagKey = source.FlagKey 
           AND target.ApplicationName = source.ApplicationName 
           AND target.ApplicationVersion = source.ApplicationVersion
        WHEN MATCHED THEN
            UPDATE SET 
                IsPermanent = {3},
                ExpirationDate = {4},
                Tags = {5}
        WHEN NOT MATCHED THEN
            INSERT (FlagKey, ApplicationName, ApplicationVersion, IsPermanent, ExpirationDate, Tags)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5});

        INSERT INTO FeatureFlagsAudit (
            FlagKey, ApplicationName, ApplicationVersion,
            [Action], Actor, Reason, [Timestamp]
        ) VALUES ({0}, {1}, {2}, 'metadata-updated', {6}, {7}, {8});",
            identifier.Key,
            identifier.ApplicationName ?? "global",
            identifier.ApplicationVersion ?? "0.0.0.0",
            metadata.RetentionPolicy.IsPermanent,
			(DateTimeOffset)metadata.RetentionPolicy.ExpirationDate,
            JsonSerializer.Serialize(metadata.Tags),
            metadata.LastModified?.Actor ?? "anonymous",
            metadata.LastModified?.Reason ?? "Metadata updated",
			(DateTimeOffset)(metadata.LastModified?.Timestamp ?? DateTimeOffset.UtcNow));

        return flag;
    }

    public async Task<bool> DeleteAsync(FlagIdentifier identifier, string userid, string reason, CancellationToken cancellationToken = default)
    {
        var deletedRows = await context.Database.ExecuteSqlRawAsync(@"
        INSERT INTO FeatureFlagsAudit (
            FlagKey, ApplicationName, ApplicationVersion,
            [Action], Actor, Reason, [Timestamp]
        ) VALUES ({0}, {1}, {2}, 'flag-deleted', {3}, {4}, {5});

        DELETE FROM FeatureFlagsMetadata 
        WHERE FlagKey = {0} AND ApplicationName = {1} AND ApplicationVersion = {2};

        DELETE FROM FeatureFlags 
        WHERE [Key] = {0} AND ApplicationName = {1} AND ApplicationVersion = {2} AND Scope = {6};",
        identifier.Key,
        identifier.ApplicationName ?? "global",
        identifier.ApplicationVersion ?? "0.0.0.0",
        userid,
        reason,
        DateTimeOffset.UtcNow,
        (int)identifier.Scope);

        return deletedRows > 0;
    }
}
