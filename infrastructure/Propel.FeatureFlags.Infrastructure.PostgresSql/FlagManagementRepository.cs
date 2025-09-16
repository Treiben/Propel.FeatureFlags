using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Helpers;
using System.Data;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql;

public class FlagManagementRepository : IFlagManagementRepository
{
	private readonly string _connectionString;
	private readonly ILogger<FlagManagementRepository> _logger;

	public FlagManagementRepository(string connectionString, ILogger<FlagManagementRepository> logger)
	{
		_connectionString = connectionString;
		_logger = logger;
		_logger.LogDebug("PostgreSQL Feature Flag Repository initialized with connection pooling");
	}

	public async Task<FeatureFlag?> GetAsync(FlagKey flagKey, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting feature flag with key: {Key}, Scope: {Scope}, Application: {Application}",
			flagKey, flagKey.Scope, flagKey.ApplicationName);

		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(flagKey, "ff.");
		var sql = _sqlSelect + $" {whereClause}";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.AddFilterParameters(parameters);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			if (!await reader.ReadAsync(cancellationToken))
			{
				_logger.LogDebug("Feature flag with key {Key} not found within scope {Scope}", flagKey, flagKey.Scope);
				return null;
			}

			var flag = await reader.LoadAllFields();
			_logger.LogDebug("Retrieved feature flag: {Key} with evaluation modes {Modes}",
				flag.Key, string.Join(",", flag.ActiveEvaluationModes.Modes));
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error retrieving feature flag with key {Key}", flagKey);
			throw;
		}
	}

	public async Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Retrieving all feature flags");

		const string sql = _sqlSelect + @" ORDER BY ff.name";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			var flags = new List<FeatureFlag>();
			while (await reader.ReadAsync(cancellationToken))
			{
				flags.Add(await reader.LoadAllFields());
			}

			_logger.LogDebug("Retrieved {Count} feature flags", flags.Count);
			return flags;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error retrieving all feature flags");
			throw;
		}
	}

	public async Task<PagedResult<FeatureFlag>> GetPagedAsync(int page, int pageSize, FeatureFlagFilter? filter = null, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting paged feature flags - Page: {Page}, PageSize: {PageSize}", page, pageSize);

		// Validate and normalize pagination parameters
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var (whereClause, parameters) = QueryBuilders.BuildFilterConditions(filter);

		// Use window function for better performance - single query instead of separate count
		var sql = _sqlSelect + 
			$@" {whereClause} 
				ORDER BY ff.name 
				LIMIT @pageSize OFFSET @offset";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);

			command.AddFilterParameters(parameters);
			command.Parameters.AddWithValue("pageSize", pageSize);
			command.Parameters.AddWithValue("offset", (page - 1) * pageSize);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			var flags = new List<FeatureFlag>();
			int totalCount = 0;

			while (await reader.ReadAsync(cancellationToken))
			{
				if (totalCount == 0) // Set total count from first row
				{
					totalCount = await reader.GetFieldValueAsync<int>("total_count");
				}
				flags.Add(await reader.LoadAllFields());
			}

			var result = new PagedResult<FeatureFlag>
			{
				Items = flags,
				TotalCount = totalCount,
				Page = page,
				PageSize = pageSize
			};

			_logger.LogDebug("Retrieved {Count} feature flags from page {Page} of {TotalPages} (total: {TotalCount})",
				flags.Count, page, result.TotalPages, totalCount);

			return result;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error retrieving paged feature flags");
			throw;
		}
	}

	public async Task<FeatureFlag> CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Creating feature flag with key: {Key} for application: {Application}",
			flag.Key, flag.Key.ApplicationName);

		const string sql = @"
            INSERT INTO feature_flags (
                key, name, description, evaluation_modes, 
                expiration_date, scheduled_enable_date, scheduled_disable_date,
                window_start_time, window_end_time, time_zone, window_days,
                user_percentage_enabled, targeting_rules, enabled_users, disabled_users,
                enabled_tenants, disabled_tenants, tenant_percentage_enabled,
                variations, default_variation, tags, is_permanent,
                application_name, application_version, scope
            ) VALUES (
                @key, @name, @description, @evaluation_modes,
                @expiration_date, @scheduled_enable_date, @scheduled_disable_date,
                @window_start_time, @window_end_time, @time_zone, @window_days,
                @user_percentage_enabled, @targeting_rules, @enabled_users, @disabled_users,
                @enabled_tenants, @disabled_tenants, @tenant_percentage_enabled,
                @variations, @default_variation, @tags, @is_permanent,
                @application_name, @application_version, @scope
            );";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			bool flagAlreadyCreated = await FlagAuditHelpers.FlagAlreadyCreated(flag.Key, connection, cancellationToken);
			if (flagAlreadyCreated)
			{
				throw new DuplicatedFeatureFlagException(flag.Key.Key, flag.Key.Scope, flag.Key.ApplicationName, flag.Key.ApplicationVersion);
			}

			using var command = new NpgsqlCommand(sql, connection);
			command.AddAllParameters(flag);

			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
			if (rowsAffected == 0)
			{
				throw new FlagInsertException("Failed to add a feature flag: inserted 0 records",
					flag.Key.Key, flag.Key.Scope, flag.Key.ApplicationName, flag.Key.ApplicationVersion);
			}

			await AddAuditTrail(flag.Key, "flag created", flag.Created, connection, cancellationToken);

			_logger.LogDebug("Successfully created feature flag: {Key}", flag.Key);
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException && ex is not FlagInsertException && ex is not DuplicatedFeatureFlagException)
		{
			_logger.LogError(ex, "Error creating feature flag with key {Key} {Scope} {Application} {Version}", 
				flag.Key, flag.Key.Scope, flag.Key.ApplicationName, flag.Key.ApplicationVersion);

			throw new FlagInsertException("Error creating feature flag", ex, 
				flag.Key.Key, flag.Key.Scope, flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		}
	}

	public async Task<FeatureFlag> UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Updating feature flag with key: {Key}", flag.Key);

		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(flag.Key);

		var sql = $@"
            UPDATE feature_flags SET 
                name = @name, 
				description = @description, 
				evaluation_modes = @evaluation_modes, 
                expiration_date = @expiration_date, 
				scheduled_enable_date = @scheduled_enable_date, 
                scheduled_disable_date = @scheduled_disable_date,
                window_start_time = @window_start_time, 
				window_end_time = @window_end_time, 
                time_zone = @time_zone, 
				window_days = @window_days,
                user_percentage_enabled = @user_percentage_enabled, 
				targeting_rules = @targeting_rules, 
                enabled_users = @enabled_users, 
				disabled_users = @disabled_users,
                enabled_tenants = @enabled_tenants, 
				disabled_tenants = @disabled_tenants, 
                tenant_percentage_enabled = @tenant_percentage_enabled,
                variations = @variations, 
				default_variation = @default_variation, 
                tags = @tags, 
				is_permanent = @is_permanent
            {whereClause};";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.AddAllParameters(flag);
			command.AddFilterParameters(parameters);

			await connection.OpenAsync(cancellationToken);
			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

			if (rowsAffected == 0)
			{
				throw new FlagUpdateException("Flag not found: updated 0 records", 
					flag.Key.Key, flag.Key.Scope, flag.Key.ApplicationName, flag.Key.ApplicationVersion);
			}

			await AddAuditTrail(flag.Key, "flag modified", flag.LastModified, connection, cancellationToken);

			_logger.LogDebug("Successfully updated feature flag: {Key}", flag.Key);
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException && ex is not FlagUpdateException)
		{
			_logger.LogError(ex, "Error updating feature flag with key {Key} {Scope} {Application} {Version}",
				flag.Key.Key, flag.Key.Scope, flag.Key.ApplicationName, flag.Key.ApplicationVersion);

			throw new FlagUpdateException("Error updating feature flag", ex, flag.Key.Key, flag.Key.Scope,flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		}
	}

	public async Task<bool> DeleteAsync(FlagKey flagKey, string actor, string reason, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Deleting feature flag with key: {Key}", flagKey.Key);

		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(flagKey);
		var sql = @$"DELETE FROM feature_flags {whereClause};";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.AddFilterParameters(parameters);

			await connection.OpenAsync(cancellationToken);
			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
			var deleted = rowsAffected > 0;

			if (deleted)
			{
				await AddAuditTrail(flagKey, "flag deleted", new AuditTrail(DateTime.UtcNow, actor, reason), connection, cancellationToken);
			} 
			else
				_logger.LogWarning("Feature flag with key {Key} not found for deletion", flagKey.Key);

			return deleted;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error deleting feature flag with key {Key} {Scope} {Application} {Version}",
				flagKey.Key, flagKey.Scope, flagKey.ApplicationName, flagKey.ApplicationVersion);
			throw;
		}
	}

	private async Task AddAuditTrail(FlagKey flagKey,
									string action,
									AuditTrail? lastModified,
									NpgsqlConnection connection,
									CancellationToken cancellationToken)
	{
		const string sql = @"-- Audit log entry
					INSERT INTO feature_flag_audit (
						flag_key, application_name, application_version, action, actor, timestamp, reason
					) VALUES (
						@key, @application_name, @application_version, @action, @actor, @timestamp, @reason
					);";

		using var command = new NpgsqlCommand(sql, connection);
		command.AddAuditParameters(flagKey,
			action: action,
			actor: lastModified?.Actor ?? "anonymous",
			reason: lastModified?.Reason ?? "not specified");

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private const string sqlSelectColumns = @"
        ff.key,
		ff.name, 
		ff.description, 
		ff.evaluation_modes, 
        ff.expiration_date, 
		ff.scheduled_enable_date, 
		ff.scheduled_disable_date,
        ff.window_start_time, 
		ff.window_end_time, 
		ff.time_zone, 
		ff.window_days,
        ff.user_percentage_enabled, 
		ff.targeting_rules, 
		ff.enabled_users, 
		ff.disabled_users,
        ff.enabled_tenants, 
		ff.disabled_tenants, 
		ff.tenant_percentage_enabled,
        ff.variations, 
		ff.default_variation, 
		ff.tags, 
		ff.is_permanent,
        ff.application_name, 
		ff.application_version,
		ff.scope,
		COUNT(ff.*) OVER() as total_count,
		created_audit.actor as created_by,
		created_audit.timestamp as created_at,
		created_audit.reason as creation_reason,
		modified_audit.actor as updated_by,
		modified_audit.timestamp as updated_at,
		modified_audit.reason as modification_reason"
;

	private const string _sqlSelect = "SELECT " + sqlSelectColumns + @" 
			FROM feature_flags ff
			LEFT JOIN (
				SELECT DISTINCT ON (flag_key, application_name, COALESCE(application_version, ''))
					   flag_key, application_name, application_version, actor, timestamp, reason
				FROM feature_flag_audit 
				WHERE action = 'flag created'
				ORDER BY flag_key, application_name, COALESCE(application_version, ''), timestamp ASC
			) created_audit ON ff.key = created_audit.flag_key 
				AND ff.application_name = created_audit.application_name 
				AND COALESCE(ff.application_version, '') = COALESCE(created_audit.application_version, '')
			LEFT JOIN (
				SELECT DISTINCT ON (flag_key, application_name, COALESCE(application_version, ''))
					   flag_key, application_name, application_version, actor, timestamp, reason
				FROM feature_flag_audit 
				WHERE action = 'flag modified'
				ORDER BY flag_key, application_name, COALESCE(application_version, ''), timestamp DESC
			) modified_audit ON ff.key = modified_audit.flag_key 
				AND ff.application_name = modified_audit.application_name 
				AND COALESCE(ff.application_version, '') = COALESCE(modified_audit.application_version, '')";
}
