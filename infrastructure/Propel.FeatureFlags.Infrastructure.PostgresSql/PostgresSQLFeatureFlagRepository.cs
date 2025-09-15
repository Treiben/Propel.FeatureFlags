using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Data;
using System.Text.Json;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql;

public class PostgreSQLFeatureFlagRepository : IFeatureFlagRepository
{
	private readonly string _connectionString;
	private readonly ILogger<PostgreSQLFeatureFlagRepository> _logger;

	public PostgreSQLFeatureFlagRepository(string connectionString, ILogger<PostgreSQLFeatureFlagRepository> logger)
	{
		_connectionString = connectionString;
		_logger = logger;
		_logger.LogDebug("PostgreSQL Feature Flag Repository initialized with connection pooling");
	}

	public async Task<FeatureFlag?> GetAsync(FlagKey key, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting feature flag with key: {Key}, Scope: {Scope}, Application: {Application}",
			key, key.Scope, key.ApplicationName);

		var (whereClause, parameters) = BuildWhereClause(key);
		var sql = $"SELECT * FROM feature_flags {whereClause}";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.AddFilterParameters(parameters);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			if (!await reader.ReadAsync(cancellationToken))
			{
				_logger.LogDebug("Feature flag with key {Key} not found within scope {Scope}", key, key.Scope);
				return null;
			}

			var flag = await reader.LoadFlagFromReader();
			_logger.LogDebug("Retrieved feature flag: {Key} with evaluation modes {Modes}",
				flag.Key, string.Join(",", flag.ActiveEvaluationModes.Modes));
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error retrieving feature flag with key {Key}", key);
			throw;
		}
	}

	public async Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Retrieving all feature flags");

		const string sql = "SELECT * FROM feature_flags ORDER BY name";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			var flags = new List<FeatureFlag>();
			while (await reader.ReadAsync(cancellationToken))
			{
				flags.Add(await reader.LoadFlagFromReader());
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

		var (whereClause, parameters) = BuildFilterConditions(filter);

		// Use window function for better performance - single query instead of separate count
		var sql = $@"
            SELECT *, COUNT(*) OVER() as total_count 
            FROM feature_flags {whereClause} 
            ORDER BY name 
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
				flags.Add(await reader.LoadFlagFromReader());
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
			flag.Key, flag.Retention.ApplicationName);

		// Validate uniqueness
		await ValidateUniqueFlagAsync(flag.ToFlagKey(), cancellationToken);

		const string sql = @"
            INSERT INTO feature_flags (
                key, name, description, evaluation_modes, created_at, updated_at, created_by, updated_by,
                expiration_date, scheduled_enable_date, scheduled_disable_date,
                window_start_time, window_end_time, time_zone, window_days,
                user_percentage_enabled, targeting_rules, enabled_users, disabled_users,
                enabled_tenants, disabled_tenants, tenant_percentage_enabled,
                variations, default_variation, tags, is_permanent,
                application_name, application_version, scope
            ) VALUES (
                @key, @name, @description, @evaluation_modes, @created_at, @updated_at, @created_by, @updated_by,
                @expiration_date, @scheduled_enable_date, @scheduled_disable_date,
                @window_start_time, @window_end_time, @time_zone, @window_days,
                @user_percentage_enabled, @targeting_rules, @enabled_users, @disabled_users,
                @enabled_tenants, @disabled_tenants, @tenant_percentage_enabled,
                @variations, @default_variation, @tags, @is_permanent,
                @application_name, @application_version, @scope
            )";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.AddParameters(flag);

			await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);

			_logger.LogDebug("Successfully created feature flag: {Key}", flag.Key);
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error creating feature flag with key {Key}", flag.Key);
			throw;
		}
	}

	public async Task<FeatureFlag> UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Updating feature flag with key: {Key}", flag.Key);

		var (whereClause, parameters) = BuildWhereClause(flag.ToFlagKey());

		var sql = $@"
            UPDATE feature_flags SET 
                name = @name, description = @description, evaluation_modes = @evaluation_modes, 
                updated_at = @updated_at, updated_by = @updated_by,
                expiration_date = @expiration_date, scheduled_enable_date = @scheduled_enable_date, 
                scheduled_disable_date = @scheduled_disable_date,
                window_start_time = @window_start_time, window_end_time = @window_end_time, 
                time_zone = @time_zone, window_days = @window_days,
                user_percentage_enabled = @user_percentage_enabled, targeting_rules = @targeting_rules, 
                enabled_users = @enabled_users, disabled_users = @disabled_users,
                enabled_tenants = @enabled_tenants, disabled_tenants = @disabled_tenants, 
                tenant_percentage_enabled = @tenant_percentage_enabled,
                variations = @variations, default_variation = @default_variation, 
                tags = @tags, is_permanent = @is_permanent 
            {whereClause}";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.AddParameters(flag);
			command.AddFilterParameters(parameters);

			await connection.OpenAsync(cancellationToken);
			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

			if (rowsAffected == 0)
			{
				var message = $"Feature flag '{flag.Key}' not found";
				_logger.LogWarning(message);
				throw new InvalidOperationException(message);
			}

			_logger.LogDebug("Successfully updated feature flag: {Key}", flag.Key);
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error updating feature flag with key {Key}", flag.Key);
			throw;
		}
	}

	public async Task<bool> DeleteAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Deleting feature flag with key: {Key}", flag.Key);

		var (whereClause, parameters) = BuildWhereClause(flag.ToFlagKey());
		var sql = $"DELETE FROM feature_flags {whereClause}";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.AddFilterParameters(parameters);

			await connection.OpenAsync(cancellationToken);
			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
			var deleted = rowsAffected > 0;

			if (deleted)
				_logger.LogDebug("Successfully deleted feature flag: {Key}", flag.Key);
			else
				_logger.LogDebug("Feature flag with key {Key} not found for deletion", flag.Key);

			return deleted;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error deleting feature flag with key {Key}", flag.Key);
			throw;
		}
	}

	private async Task ValidateUniqueFlagAsync(FlagKey key, CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = BuildWhereClause(key);
		var sql = $"SELECT COUNT(*) FROM feature_flags {whereClause}";

		using var connection = new NpgsqlConnection(_connectionString);
		using var command = new NpgsqlCommand(sql, connection);
		command.AddFilterParameters(parameters);

		await connection.OpenAsync(cancellationToken);
		var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

		if (count > 0)
		{
			var message = "Feature flag with key '{Key}' already exists in scope '{Scope}' for application '{ApplicationName}'";
			_logger.LogWarning(message, key.Key, key.Scope, key.ApplicationName);
			throw new DuplicatedFeatureFlagException(key.Key, key.Scope, key.ApplicationName);
		}
	}

	private static (string whereClause, Dictionary<string, object> parameters) BuildWhereClause(FlagKey key)
	{
		var parameters = new Dictionary<string, object>
		{
			["key"] = key.Key,
			["scope"] = (int)key.Scope
		};

		if (key.Scope == Scope.Global)
		{
			return ("WHERE key = @key AND scope = @scope", parameters);
		}

		// Application or Feature scope
		if (string.IsNullOrEmpty(key.ApplicationName))
		{
			throw new ArgumentException("Application name required for application-scoped flags.", nameof(key.ApplicationName));
		}

		parameters["application_name"] = key.ApplicationName;

		if (!string.IsNullOrEmpty(key.ApplicationVersion))
		{
			parameters["application_version"] = key.ApplicationVersion;
			return ("WHERE key = @key AND scope = @scope AND application_name = @application_name AND application_version = @application_version", parameters);
		}
		else
		{
			return ("WHERE key = @key AND scope = @scope AND application_name = @application_name AND application_version IS NULL", parameters);
		}
	}

	private static (string whereClause, Dictionary<string, object> parameters) BuildFilterConditions(FeatureFlagFilter? filter)
	{
		var conditions = new List<string>();
		var parameters = new Dictionary<string, object>();

		if (filter == null)
			return (string.Empty, parameters);

		// Scope filtering
		if (filter.Scope.HasValue)
		{
			conditions.Add("scope = @scope");
			parameters["scope"] = (int)filter.Scope.Value;
		}

		// Application filtering
		if (!string.IsNullOrEmpty(filter.ApplicationName))
		{
			conditions.Add("application_name = @application_name");
			parameters["application_name"] = filter.ApplicationName;
		}

		// Evaluation modes filtering
		if (filter.EvaluationModes != null && filter.EvaluationModes.Length > 0)
		{
			var modeConditions = new List<string>();
			for (int i = 0; i < filter.EvaluationModes.Length; i++)
			{
				var modeParam = $"mode{i}";
				modeConditions.Add($"evaluation_modes @> @{modeParam}");
				parameters[modeParam] = JsonSerializer.Serialize(new[] { (int)filter.EvaluationModes[i] }, JsonDefaults.JsonOptions);
			}
			conditions.Add($"({string.Join(" OR ", modeConditions)})");
		}

		// Expiration filtering
		if (filter.ExpiringInDays.HasValue && filter.ExpiringInDays.Value > 0)
		{
			var expiryDate = DateTime.UtcNow.AddDays(filter.ExpiringInDays.Value);
			conditions.Add("expiration_date <= @expiryDate AND is_permanent = false");
			parameters["expiryDate"] = expiryDate;
		}

		// Tags filtering
		if (filter.Tags != null && filter.Tags.Count > 0)
		{
			var tagConditions = new List<string>();
			var tagIndex = 0;

			foreach (var tag in filter.Tags)
			{
				if (string.IsNullOrEmpty(tag.Value))
				{
					// Search by tag key only
					var keyParam = $"tagKey{tagIndex}";
					tagConditions.Add($"tags ? @{keyParam}");
					parameters[keyParam] = tag.Key;
				}
				else
				{
					// Search by exact key-value match
					var tagParam = $"tag{tagIndex}";
					tagConditions.Add($"tags @> @{tagParam}");
					parameters[tagParam] = JsonSerializer.Serialize(new Dictionary<string, string> { [tag.Key] = tag.Value }, JsonDefaults.JsonOptions);
				}
				tagIndex++;
			}

			if (tagConditions.Count > 0)
			{
				conditions.Add($"({string.Join(" OR ", tagConditions)})");
			}
		}

		var whereClause = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : string.Empty;
		return (whereClause, parameters);
	}
}

public static class NpgsqlCommandExtensions
{
	public static void AddParameters(this NpgsqlCommand command, FeatureFlag flag)
	{
		// Basic string parameters
		command.Parameters.AddWithValue("key", flag.Key);
		command.Parameters.AddWithValue("name", flag.Name);
		command.Parameters.AddWithValue("description", flag.Description);

		// Audit parameters
		command.Parameters.AddWithValue("created_at", flag.Created.Timestamp);
		command.Parameters.AddWithValue("created_by", flag.Created.Actor ?? "system");
		command.Parameters.AddWithValue("updated_at", (object?)flag.LastModified?.Timestamp ?? DBNull.Value);
		command.Parameters.AddWithValue("updated_by", (object?)flag.LastModified?.Actor ?? DBNull.Value);

		// Retention parameters
		command.Parameters.AddWithValue("expiration_date", (object?)flag.Retention.ExpirationDate ?? DBNull.Value);
		command.Parameters.AddWithValue("is_permanent", flag.Retention.IsPermanent);
		command.Parameters.AddWithValue("application_name", (object?)flag.Retention.ApplicationName ?? DBNull.Value);
		command.Parameters.AddWithValue("application_version", (object?)flag.Retention.ApplicationVersion ?? DBNull.Value);
		command.Parameters.AddWithValue("scope", (int)flag.Retention.Scope);

		// Schedule parameters - handle domain model defaults
		if (flag.Schedule.EnableOn == DateTime.MinValue)
			command.Parameters.AddWithValue("scheduled_enable_date", DBNull.Value);
		else
			command.Parameters.AddWithValue("scheduled_enable_date", flag.Schedule.EnableOn);

		if (flag.Schedule.DisableOn == DateTime.MaxValue)
			command.Parameters.AddWithValue("scheduled_disable_date", DBNull.Value);
		else
			command.Parameters.AddWithValue("scheduled_disable_date", flag.Schedule.DisableOn);

		// Operational window parameters
		command.Parameters.AddWithValue("window_start_time", flag.OperationalWindow.StartOn);
		command.Parameters.AddWithValue("window_end_time", flag.OperationalWindow.StopOn);
		command.Parameters.AddWithValue("time_zone", flag.OperationalWindow.TimeZone);

		// Access control parameters
		command.Parameters.AddWithValue("user_percentage_enabled", flag.UserAccessControl.RolloutPercentage);
		command.Parameters.AddWithValue("tenant_percentage_enabled", flag.TenantAccessControl.RolloutPercentage);

		// Variations
		command.Parameters.AddWithValue("default_variation", flag.Variations.DefaultVariation);

		// JSONB parameters require explicit type specification
		var evaluationModesParam = command.Parameters.Add("evaluation_modes", NpgsqlDbType.Jsonb);
		evaluationModesParam.Value = JsonSerializer.Serialize(flag.ActiveEvaluationModes.Modes.Select(m => (int)m), JsonDefaults.JsonOptions);

		var windowDaysParam = command.Parameters.Add("window_days", NpgsqlDbType.Jsonb);
		windowDaysParam.Value = JsonSerializer.Serialize(flag.OperationalWindow.DaysActive.Select(d => (int)d), JsonDefaults.JsonOptions);

		var targetingRulesParam = command.Parameters.Add("targeting_rules", NpgsqlDbType.Jsonb);
		targetingRulesParam.Value = JsonSerializer.Serialize(flag.TargetingRules, JsonDefaults.JsonOptions);

		var enabledUsersParam = command.Parameters.Add("enabled_users", NpgsqlDbType.Jsonb);
		enabledUsersParam.Value = JsonSerializer.Serialize(flag.UserAccessControl.Allowed, JsonDefaults.JsonOptions);

		var disabledUsersParam = command.Parameters.Add("disabled_users", NpgsqlDbType.Jsonb);
		disabledUsersParam.Value = JsonSerializer.Serialize(flag.UserAccessControl.Blocked, JsonDefaults.JsonOptions);

		var enabledTenantsParam = command.Parameters.Add("enabled_tenants", NpgsqlDbType.Jsonb);
		enabledTenantsParam.Value = JsonSerializer.Serialize(flag.TenantAccessControl.Allowed, JsonDefaults.JsonOptions);

		var disabledTenantsParam = command.Parameters.Add("disabled_tenants", NpgsqlDbType.Jsonb);
		disabledTenantsParam.Value = JsonSerializer.Serialize(flag.TenantAccessControl.Blocked, JsonDefaults.JsonOptions);

		var variationsParam = command.Parameters.Add("variations", NpgsqlDbType.Jsonb);
		variationsParam.Value = JsonSerializer.Serialize(flag.Variations.Values, JsonDefaults.JsonOptions);

		var tagsParam = command.Parameters.Add("tags", NpgsqlDbType.Jsonb);
		tagsParam.Value = JsonSerializer.Serialize(flag.Tags, JsonDefaults.JsonOptions);
	}

	public static void AddFilterParameters(this NpgsqlCommand command, Dictionary<string, object> parameters)
	{
		foreach (var (key, value) in parameters)
		{
			if (key.StartsWith("tag") && !key.StartsWith("tagKey"))
			{
				// JSONB parameter for tag values
				var parameter = command.Parameters.Add(key, NpgsqlDbType.Jsonb);
				parameter.Value = value;
			}
			else if (key.StartsWith("mode"))
			{
				// JSONB parameter for evaluation modes
				var parameter = command.Parameters.Add(key, NpgsqlDbType.Jsonb);
				parameter.Value = value;
			}
			else
			{
				command.Parameters.AddWithValue(key, value);
			}
		}
	}
}

public static class NpgsqlDataReaderExtensions
{
	public static async Task<T> DeserializeAsync<T>(this NpgsqlDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		if (await reader.IsDBNullAsync(ordinal))
			return default!;

		var json = await reader.GetFieldValueAsync<string>(ordinal);
		return JsonSerializer.Deserialize<T>(json, JsonDefaults.JsonOptions) ?? default!;
	}

	public static async Task<T> GetFieldValueOrDefaultAsync<T>(this NpgsqlDataReader reader, string columnName, T defaultValue = default!)
	{
		var ordinal = reader.GetOrdinal(columnName);
		return await reader.IsDBNullAsync(ordinal)
			? defaultValue
			: await reader.GetFieldValueAsync<T>(ordinal);
	}

	public static async Task<FeatureFlag> LoadFlagFromReader(this NpgsqlDataReader reader)
	{
		// Load evaluation modes
		var evaluationModes = await reader.DeserializeAsync<int[]>("evaluation_modes");
		var evaluationModeSet = new EvaluationModes([.. evaluationModes.Select(m => (EvaluationMode)m)]);

		// Load retention policy
		var retention = new RetentionPolicy(
			isPermanent: await reader.GetFieldValueAsync<bool>("is_permanent"),
			expirationDate: await reader.GetFieldValueOrDefaultAsync<DateTime?>("expiration_date") ?? DateTime.UtcNow.AddDays(30),
			scope: (Scope)(await reader.GetFieldValueAsync<int>("scope")),
			applicationName: await reader.GetFieldValueOrDefaultAsync<string?>("application_name"),
			applicationVersion: await reader.GetFieldValueOrDefaultAsync<string?>("application_version")
		);

		// Load audit information
		var created = new Audit(
			timestamp: await reader.GetFieldValueAsync<DateTime>("created_at"),
			actor: await reader.GetFieldValueAsync<string>("created_by")
		);

		Audit? modified = null;
		var modifiedAt = await reader.GetFieldValueOrDefaultAsync<DateTime?>("updated_at");
		var modifiedBy = await reader.GetFieldValueOrDefaultAsync<string?>("updated_by");
		if (modifiedAt.HasValue && !string.IsNullOrEmpty(modifiedBy))
		{
			modified = new Audit(timestamp: modifiedAt.Value, actor: modifiedBy);
		}

		// Load schedule - handle DB nulls properly
		var enableOn = await reader.GetFieldValueOrDefaultAsync<DateTime?>("scheduled_enable_date");
		var disableOn = await reader.GetFieldValueOrDefaultAsync<DateTime?>("scheduled_disable_date");

		var schedule = new ActivationSchedule(
			enableOn: enableOn ?? DateTime.MinValue,
			disableOn: disableOn ?? DateTime.MaxValue
		);

		// Load operational window
		var windowDaysData = await reader.DeserializeAsync<int[]>("window_days");
		var windowDays = windowDaysData?.Select(d => (DayOfWeek)d).ToArray();

		var operationalWindow = new OperationalWindow(
			startOn: await reader.GetFieldValueOrDefaultAsync<TimeSpan>("window_start_time"),
			stopOn: await reader.GetFieldValueOrDefaultAsync<TimeSpan>("window_end_time", new TimeSpan(23, 59, 59)),
			timeZone: await reader.GetFieldValueOrDefaultAsync<string>("time_zone", "UTC"),
			daysActive: windowDays
		);

		// Load access controls
		var userAccess = new AccessControl(
			allowed: await reader.DeserializeAsync<List<string>>("enabled_users"),
			blocked: await reader.DeserializeAsync<List<string>>("disabled_users"),
			rolloutPercentage: await reader.GetFieldValueAsync<int>("user_percentage_enabled")
		);

		var tenantAccess = new AccessControl(
			allowed: await reader.DeserializeAsync<List<string>>("enabled_tenants"),
			blocked: await reader.DeserializeAsync<List<string>>("disabled_tenants"),
			rolloutPercentage: await reader.GetFieldValueAsync<int>("tenant_percentage_enabled")
		);

		// Load variations
		var variations = new Variations
		{
			Values = await reader.DeserializeAsync<Dictionary<string, object>>("variations") ?? [],
			DefaultVariation = await reader.GetFieldValueOrDefaultAsync<string>("default_variation", "off")
		};

		return new FeatureFlag
		{
			Key = await reader.GetFieldValueAsync<string>("key"),
			Name = await reader.GetFieldValueAsync<string>("name"),
			Description = await reader.GetFieldValueAsync<string>("description"),
			ActiveEvaluationModes = evaluationModeSet,
			Retention = retention,
			Schedule = schedule,
			OperationalWindow = operationalWindow,
			UserAccessControl = userAccess,
			TenantAccessControl = tenantAccess,
			Variations = variations,
			TargetingRules = await reader.DeserializeAsync<List<ITargetingRule>>("targeting_rules") ?? [],
			Tags = await reader.DeserializeAsync<Dictionary<string, string>>("tags") ?? [],
			Created = created,
			LastModified = modified
		};
	}
}