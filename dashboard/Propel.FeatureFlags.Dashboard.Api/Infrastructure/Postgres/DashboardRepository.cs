using Microsoft.EntityFrameworkCore;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Text.Json;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.Postgres;

public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DashboardDbContext(options)
{
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new FeatureFlagConfiguration());
		modelBuilder.ApplyConfiguration(new FeatureFlagMetadataConfiguration());
		modelBuilder.ApplyConfiguration(new FeatureFlagAuditConfiguration());
	}
}

public class DashboardRepository(PostgresDbContext context) : BaseRepository(context), IDashboardRepository
{
	public async Task<FeatureFlag> CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		var identifier = flag.Identifier;
		var metadata = flag.Administration;
		var config = flag.EvaluationOptions;
		var lastModified = metadata.ChangeHistory[^1];

		await Context.Database.ExecuteSqlRawAsync(@"
        INSERT INTO feature_flags (
            key, application_name, application_version, scope, name, description,
            evaluation_modes, scheduled_enable_date, scheduled_disable_date,
            window_start_time, window_end_time, time_zone, window_days,
            targeting_rules, enabled_users, disabled_users, user_percentage_enabled,
            enabled_tenants, disabled_tenants, tenant_percentage_enabled,
            variations, default_variation
        ) VALUES (
            {0}, {1}, {2}, {3}, {4}, {5}, {6}::jsonb, {7}, {8}, {9}, {10}, {11}, {12}::jsonb,
            {13}::jsonb, {14}::jsonb, {15}::jsonb, {16}, {17}::jsonb, {18}::jsonb, {19}, {20}::jsonb, {21}
        );

        INSERT INTO feature_flags_metadata (
            flag_key, application_name, application_version,
            is_permanent, expiration_date, tags
        ) VALUES ({0}, {1}, {2}, {22}, {23}, {24}::jsonb);

        INSERT INTO feature_flags_audit (
            flag_key, application_name, application_version,
            action, actor, notes, timestamp
        ) VALUES ({0}, {1}, {2}, {25}, {26}, {27}, {28});",
		identifier.Key,
		identifier.ApplicationName ?? "global",
		identifier.ApplicationVersion ?? "0.0.0.0",
		(int)identifier.Scope,
		metadata.Name,
		metadata.Description,
		JsonSerializer.Serialize(config.ModeSet.Modes.Select(m => (int)m).ToArray()),
		config.Schedule.HasSchedule() ? (DateTimeOffset)config.Schedule.EnableOn : null!,
		config.Schedule.HasSchedule() ? (DateTimeOffset)config.Schedule.DisableOn : null!,
		config.OperationalWindow.HasWindow() ? config.OperationalWindow.StartOn : null!,
		config.OperationalWindow.HasWindow() ? config.OperationalWindow.StopOn : null!,
		config.OperationalWindow.HasWindow() ? config.OperationalWindow.TimeZone : null!,
		JsonSerializer.Serialize(config.OperationalWindow.DaysActive.Select(d => (int)d).ToArray()),
		JsonSerializer.Serialize(config.TargetingRules, JsonDefaults.JsonOptions),
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
		lastModified.Action,
		lastModified.Actor,
		lastModified.Notes,
		(DateTimeOffset)lastModified.Timestamp);

		return flag;
	}

	public async Task<FeatureFlag> UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		var identifier = flag.Identifier;
		var metadata = flag.Administration;
		var config = flag.EvaluationOptions;
		var lastModified = metadata.ChangeHistory[^1];

		var updatedRows = await Context.Database.ExecuteSqlRawAsync(@"
        UPDATE feature_flags SET
            name = {4}, description = {5}, evaluation_modes = {6}::jsonb,
            scheduled_enable_date = {7}, scheduled_disable_date = {8},
            window_start_time = {9}, window_end_time = {10}, time_zone = {11},
            window_days = {12}::jsonb, targeting_rules = {13}::jsonb,
            enabled_users = {14}::jsonb, disabled_users = {15}::jsonb, user_percentage_enabled = {16},
            enabled_tenants = {17}::jsonb, disabled_tenants = {18}::jsonb, tenant_percentage_enabled = {19},
            variations = {20}::jsonb, default_variation = {21}
        WHERE key = {0} AND application_name = {1} AND application_version = {2} AND scope = {3};

        INSERT INTO feature_flags_audit (
            flag_key, application_name, application_version,
            action, actor, notes, timestamp
        ) VALUES ({0}, {1}, {2}, {22}, {23}, {24}, {25});",
		identifier.Key,
		identifier.ApplicationName ?? "global",
		identifier.ApplicationVersion ?? "0.0.0.0",
		(int)identifier.Scope,
		metadata.Name,
		metadata.Description,
		JsonSerializer.Serialize(config.ModeSet.Modes.Select(m => (int)m).ToArray()),
		config.Schedule.HasSchedule() ? (DateTimeOffset)config.Schedule.EnableOn : null!,
		config.Schedule.HasSchedule() ? (DateTimeOffset)config.Schedule.DisableOn : null!,
		config.OperationalWindow.HasWindow() ? config.OperationalWindow.StartOn : null!,
		config.OperationalWindow.HasWindow() ? config.OperationalWindow.StopOn : null!,
		config.OperationalWindow.HasWindow() ? config.OperationalWindow.TimeZone : null!,
		JsonSerializer.Serialize(config.OperationalWindow.DaysActive.Select(d => (int)d).ToArray()),
		JsonSerializer.Serialize(config.TargetingRules, JsonDefaults.JsonOptions),
		JsonSerializer.Serialize(config.UserAccessControl.Allowed),
		JsonSerializer.Serialize(config.UserAccessControl.Blocked),
		config.UserAccessControl.RolloutPercentage,
		JsonSerializer.Serialize(config.TenantAccessControl.Allowed),
		JsonSerializer.Serialize(config.TenantAccessControl.Blocked),
		config.TenantAccessControl.RolloutPercentage,
		JsonSerializer.Serialize(config.Variations.Values),
		config.Variations.DefaultVariation,
		lastModified.Action,
		lastModified.Actor,
		lastModified.Notes,
		(DateTimeOffset)lastModified.Timestamp);

		if (updatedRows == 0)
		{
			throw new InvalidOperationException($"Feature flag with key '{identifier.Key}' not found");
		}

		return flag;
	}

	public async Task<FeatureFlag> UpdateMetadataAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		var identifier = flag.Identifier;
		var metadata = flag.Administration;
		var lastModified = metadata.ChangeHistory[^1];

		await Context.Database.ExecuteSqlRawAsync(@"
		UPDATE feature_flags
		SET name = {4}, description = {5}
		WHERE key = {0} AND application_name = {1} AND application_version = {2} AND scope = {3};

        UPDATE feature_flags_metadata 
        SET is_permanent = {6}, 
			expiration_date = {7}, 
			tags = {8}::jsonb
		 WHERE flag_key = {0} AND application_name = {1} AND application_version = {2};

        INSERT INTO feature_flags_audit (
            flag_key, application_name, application_version,
            action, actor, notes, timestamp
        ) VALUES ({0}, {1}, {2}, {9}, {10}, {11}, {12});",
			identifier.Key,
			identifier.ApplicationName ?? "global",
			identifier.ApplicationVersion ?? "0.0.0.0",
			identifier.Scope,
			metadata.Name,
			metadata.Description,
			metadata.RetentionPolicy.IsPermanent,
			(DateTimeOffset)metadata.RetentionPolicy.ExpirationDate,
			JsonSerializer.Serialize(metadata.Tags),
			lastModified.Action,
			lastModified.Actor,
			lastModified.Notes,
			(DateTimeOffset)lastModified.Timestamp);

		return flag;
	}

	public async Task<bool> DeleteAsync(FlagIdentifier identifier, string userid, string notes, CancellationToken cancellationToken = default)
	{
		var sql = $@"
DO $$
BEGIN
    -- Check if the record exists
    IF EXISTS (
        SELECT 1 FROM feature_flags
        WHERE key = '{identifier.Key}'
          AND application_name = '{identifier.ApplicationName ?? "global"}'
          AND application_version = '{identifier.ApplicationVersion ?? "0.0.0.0"}'
          AND scope = {(int)identifier.Scope}
    ) THEN
        -- Execute the DELETE and INSERT statements if the record exists
        DELETE FROM feature_flags_metadata
        WHERE flag_key = '{identifier.Key}'
          AND application_name = '{identifier.ApplicationName ?? "global"}'
          AND application_version = '{identifier.ApplicationVersion ?? "0.0.0.0"}';

        DELETE FROM feature_flags
        WHERE key = '{identifier.Key}'
          AND application_name = '{identifier.ApplicationName ?? "global"}'
          AND application_version = '{identifier.ApplicationVersion ?? "0.0.0.0"}'
          AND scope = {(int)identifier.Scope};

        INSERT INTO feature_flags_audit (
            flag_key, application_name, application_version,
            action, actor, notes, timestamp
        ) VALUES (
            '{identifier.Key}',
            '{identifier.ApplicationName ?? "global"}',
            '{identifier.ApplicationVersion ?? "0.0.0.0"}',
            'flag-deleted',
            '{userid}',
            '{notes}',
            '{DateTimeOffset.UtcNow}'
        );
    END IF;
END $$;";
		_ = await Context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
		return true;
	}
}
