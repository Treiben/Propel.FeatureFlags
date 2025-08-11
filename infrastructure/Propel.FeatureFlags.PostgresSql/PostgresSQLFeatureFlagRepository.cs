using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Persistence;
using System.Text.Json;

namespace Propel.FeatureFlags.PostgresSql;

public class PostgreSQLFeatureFlagRepository : IFeatureFlagRepository
{
	private readonly string _connectionString;
	private readonly ILogger<PostgreSQLFeatureFlagRepository> _logger;

	public PostgreSQLFeatureFlagRepository(string connectionString, ILogger<PostgreSQLFeatureFlagRepository> logger)
	{
		_connectionString = connectionString;
		_logger = logger;
		_logger.LogDebug("PostgreSQL Feature Flag Repository initialized");
	}

	public async Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting feature flag with key: {Key}", key);
		const string sql = @"
                SELECT key, name, description, status, created_at, updated_at, created_by, updated_by,
                       expiration_date, scheduled_enable_date, scheduled_disable_date,
                       window_start_time, window_end_time, time_zone, window_days,
                       percentage_enabled, targeting_rules, enabled_users, disabled_users,
					   enabled_tenants, disabled_tenants, tenant_percentage_enabled,
                       variations, default_variation, tags, is_permanent
                FROM feature_flags 
                WHERE key = @key";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.Parameters.AddWithValue("key", key);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			if (!await reader.ReadAsync(cancellationToken))
			{
				_logger.LogDebug("Feature flag with key {Key} not found", key);
				return null;
			}

			var flag = await MapFromReader(reader);
			_logger.LogDebug("Retrieved feature flag: {Key} with status {Status}", flag.Key, flag.Status);
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
		const string sql = @"
                SELECT key, name, description, status, created_at, updated_at, created_by, updated_by,
                       expiration_date, scheduled_enable_date, scheduled_disable_date,
                       window_start_time, window_end_time, time_zone, window_days,
                       percentage_enabled, targeting_rules, enabled_users, disabled_users,
					   enabled_tenants, disabled_tenants, tenant_percentage_enabled,
                       variations, default_variation, tags, is_permanent
                FROM feature_flags 
                ORDER BY name";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			var flags = new List<FeatureFlag>();
			while (await reader.ReadAsync(cancellationToken))
			{
				flags.Add(await MapFromReader(reader));
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

	public async Task<FeatureFlag> CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Creating feature flag with key: {Key}", flag.Key);
		const string sql = @"
                INSERT INTO feature_flags (
                    key, name, description, status, created_at, updated_at, created_by, updated_by,
                    expiration_date, scheduled_enable_date, scheduled_disable_date,
                    window_start_time, window_end_time, time_zone, window_days,
                    percentage_enabled, targeting_rules, enabled_users, disabled_users,
					enabled_tenants, disabled_tenants, tenant_percentage_enabled,
                    variations, default_variation, tags, is_permanent
                ) VALUES (
                    @key, @name, @description, @status, @created_at, @updated_at, @created_by, @updated_by,
                    @expiration_date, @scheduled_enable_date, @scheduled_disable_date,
                    @window_start_time, @window_end_time, @time_zone, @window_days,
                    @percentage_enabled, @targeting_rules, @enabled_users, @disabled_users,
					@enabled_tenants, @disabled_tenants, @tenant_percentage_enabled,
                    @variations, @default_variation, @tags, @is_permanent
                )";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			AddParameters(command, flag);

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

		flag.UpdatedAt = DateTime.UtcNow;

		const string sql = @"
                UPDATE feature_flags SET 
                    name = @name, description = @description, status = @status, updated_at = @updated_at, updated_by = @updated_by,
                    expiration_date = @expiration_date, scheduled_enable_date = @scheduled_enable_date, scheduled_disable_date = @scheduled_disable_date,
                    window_start_time = @window_start_time, window_end_time = @window_end_time, time_zone = @time_zone, window_days = @window_days,
                    percentage_enabled = @percentage_enabled, targeting_rules = @targeting_rules, enabled_users = @enabled_users, disabled_users = @disabled_users,
					enabled_tenants = @enabled_tenants, disabled_tenants = @disabled_tenants, tenant_percentage_enabled = @tenant_percentage_enabled,
                    variations = @variations, default_variation = @default_variation, tags = @tags, is_permanent = @is_permanent
                WHERE key = @key";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			AddParameters(command, flag);

			await connection.OpenAsync(cancellationToken);
			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
			if (rowsAffected == 0)
			{
				_logger.LogError("Feature flag '{Key}' not found during update", flag.Key);
				throw new InvalidOperationException($"Feature flag '{flag.Key}' not found");
			}

			_logger.LogDebug("Successfully updated feature flag: {Key}", flag.Key);
			return flag;
		}
		catch (Exception ex) when (ex is not InvalidOperationException && ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error updating feature flag with key {Key}", flag.Key);
			throw;
		}
	}

	public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Deleting feature flag with key: {Key}", key);
		const string sql = "DELETE FROM feature_flags WHERE key = @key";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.Parameters.AddWithValue("key", key);

			await connection.OpenAsync(cancellationToken);
			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
			var deleted = rowsAffected > 0;

			if (deleted)
				_logger.LogDebug("Successfully deleted feature flag: {Key}", key);
			else
				_logger.LogDebug("Feature flag with key {Key} not found for deletion", key);

			return deleted;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error deleting feature flag with key {Key}", key);
			throw;
		}
	}

	public async Task<List<FeatureFlag>> GetExpiringAsync(DateTime before, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting feature flags expiring before: {ExpiryDate}", before);
		const string sql = @"
                SELECT key, name, description, status, created_at, updated_at, created_by, updated_by,
                       expiration_date, scheduled_enable_date, scheduled_disable_date,
                       window_start_time, window_end_time, time_zone, window_days,
                       percentage_enabled, targeting_rules, enabled_users, disabled_users,
					   enabled_tenants, disabled_tenants, tenant_percentage_enabled,
                       variations, default_variation, tags, is_permanent
                FROM feature_flags 
                WHERE expiration_date <= @before AND is_permanent = false
                ORDER BY expiration_date";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.Parameters.AddWithValue("before", before);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			var flags = new List<FeatureFlag>();
			while (await reader.ReadAsync(cancellationToken))
			{
				flags.Add(await MapFromReader(reader));
			}

			_logger.LogDebug("Retrieved {Count} expiring feature flags", flags.Count);
			return flags;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error retrieving expiring feature flags before {ExpiryDate}", before);
			throw;
		}
	}

	public async Task<List<FeatureFlag>> GetByTagsAsync(Dictionary<string, string> tags, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting feature flags by tags: {@Tags}", tags);
		// Implementation for tag-based queries using PostgreSQL JSONB operations
		var sql = "SELECT * FROM feature_flags WHERE ";

		var conditions = new List<string>();
		for (int i = 0; i < tags.Count; i++)
		{
			var tag = tags.ElementAt(i);
			if (string.IsNullOrEmpty(tag.Value))
			{
				// Search by tag key only when value is null or empty
				conditions.Add($"tags ? @tagKey{i}");
			}
			else
			{
				// Search by exact key-value match
				conditions.Add($"tags @> @tag{i}");
			}
		}

		sql += string.Join(" AND ", conditions);

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			for (int i = 0; i < tags.Count; i++)
			{
				var tag = tags.ElementAt(i);
				if (string.IsNullOrEmpty(tag.Value))
				{
					// Parameter for key-only search
					command.Parameters.AddWithValue($"tagKey{i}", tag.Key);
				}
				else
				{
					// Parameter for key-value search
					var parameter = command.Parameters.Add($"tag{i}", NpgsqlDbType.Jsonb);
					parameter.Value = JsonSerializer.Serialize(new Dictionary<string, string> { [tag.Key] = tag.Value });
				}
			}

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			var flags = new List<FeatureFlag>();
			while (await reader.ReadAsync(cancellationToken))
			{
				flags.Add(await MapFromReader(reader));
			}

			_logger.LogDebug("Retrieved {Count} feature flags matching tags {@Tags}", flags.Count, tags);
			return flags;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error retrieving feature flags by tags {@Tags}", tags);
			throw;
		}
	}

	private static async Task<FeatureFlag> MapFromReader(NpgsqlDataReader reader)
	{
		return new FeatureFlag
		{
			Key = await reader.GetDataAsync<string>("key"),
			Name = await reader.GetDataAsync<string>("name"),
			Description = await reader.GetDataAsync<string>("description"),
			Status = (FeatureFlagStatus)await reader.GetDataAsync<int>("status"),
			CreatedAt = await reader.GetDataAsync<DateTime>("created_at"),
			UpdatedAt = await reader.GetDataAsync<DateTime>("updated_at"),
			CreatedBy = await reader.GetDataAsync<string>("created_by"),
			UpdatedBy = await reader.GetDataAsync<string>("updated_by"),
			ExpirationDate = await reader.GetDataAsync<DateTime>("expiration_date"),
			ScheduledEnableDate = await reader.GetDataAsync<DateTime>("scheduled_enable_date"),
			ScheduledDisableDate = await reader.GetDataAsync<DateTime>("scheduled_disable_date"),
			WindowStartTime = await reader.GetTimeOnly("window_start_time"),
			WindowEndTime = await reader.GetTimeOnly("window_end_time"),
			TimeZone = await reader.GetDataAsync<string>("time_zone"),
			WindowDays = await reader.Deserialize<List<DayOfWeek>>("window_days"),
			PercentageEnabled = await reader.GetDataAsync<int>("percentage_enabled"),
			TargetingRules = await reader.Deserialize<List<TargetingRule>>("targeting_rules"),
			EnabledUsers = await reader.Deserialize<List<string>>("enabled_users"),
			DisabledUsers = await reader.Deserialize<List<string>>("disabled_users"),
			EnabledTenants = await reader.Deserialize<List<string>>("enabled_tenants"),
			DisabledTenants = await reader.Deserialize<List<string>>("disabled_tenants"),
			TenantPercentageEnabled = await reader.GetDataAsync<int>("tenant_percentage_enabled"),
			Variations = await reader.Deserialize<Dictionary<string, object>>("variations"),
			DefaultVariation = await reader.GetDataAsync<string>("default_variation"),
			Tags = await reader.Deserialize<Dictionary<string, string>>("tags"),
			IsPermanent = await reader.GetDataAsync<bool>("is_permanent")
		};
	}

	private static void AddParameters(NpgsqlCommand command, FeatureFlag flag)
	{
		command.Parameters.AddWithValue("key", flag.Key);
		command.Parameters.AddWithValue("name", flag.Name);
		command.Parameters.AddWithValue("description", flag.Description);
		command.Parameters.AddWithValue("status", (int)flag.Status);
		command.Parameters.AddWithValue("created_at", flag.CreatedAt);
		command.Parameters.AddWithValue("updated_at", flag.UpdatedAt);
		command.Parameters.AddWithValue("created_by", flag.CreatedBy);
		command.Parameters.AddWithValue("updated_by", flag.UpdatedBy);
		command.Parameters.AddWithValue("expiration_date", (object?)flag.ExpirationDate ?? DBNull.Value);
		command.Parameters.AddWithValue("scheduled_enable_date", (object?)flag.ScheduledEnableDate ?? DBNull.Value);
		command.Parameters.AddWithValue("scheduled_disable_date", (object?)flag.ScheduledDisableDate ?? DBNull.Value);
		command.Parameters.AddWithValue("window_start_time", (object?)flag.WindowStartTime ?? DBNull.Value);
		command.Parameters.AddWithValue("window_end_time", (object?)flag.WindowEndTime ?? DBNull.Value);
		command.Parameters.AddWithValue("time_zone", (object?)flag.TimeZone ?? DBNull.Value);
		
		// JSONB parameters require explicit type specification
		var windowDaysParam = command.Parameters.Add("window_days", NpgsqlDbType.Jsonb);
		windowDaysParam.Value = JsonSerializer.Serialize(flag.WindowDays ?? new());
		
		command.Parameters.AddWithValue("percentage_enabled", flag.PercentageEnabled);
		command.Parameters.AddWithValue("tenant_percentage_enabled", flag.TenantPercentageEnabled);

		var targetingRulesParam = command.Parameters.Add("targeting_rules", NpgsqlDbType.Jsonb);
		targetingRulesParam.Value = JsonSerializer.Serialize(flag.TargetingRules);
		
		var enabledUsersParam = command.Parameters.Add("enabled_users", NpgsqlDbType.Jsonb);
		enabledUsersParam.Value = JsonSerializer.Serialize(flag.EnabledUsers);
		
		var disabledUsersParam = command.Parameters.Add("disabled_users", NpgsqlDbType.Jsonb);
		disabledUsersParam.Value = JsonSerializer.Serialize(flag.DisabledUsers);

		var enabledTenantsParam = command.Parameters.Add("enabled_tenants", NpgsqlDbType.Jsonb);
		enabledTenantsParam.Value = JsonSerializer.Serialize(flag.EnabledTenants);

		var disabledTenantsParam = command.Parameters.Add("disabled_tenants", NpgsqlDbType.Jsonb);
		disabledTenantsParam.Value = JsonSerializer.Serialize(flag.DisabledTenants);

		var variationsParam = command.Parameters.Add("variations", NpgsqlDbType.Jsonb);
		variationsParam.Value = JsonSerializer.Serialize(flag.Variations);
		
		command.Parameters.AddWithValue("default_variation", flag.DefaultVariation);
		
		var tagsParam = command.Parameters.Add("tags", NpgsqlDbType.Jsonb);
		tagsParam.Value = JsonSerializer.Serialize(flag.Tags);
		
		command.Parameters.AddWithValue("is_permanent", flag.IsPermanent);
	}
}

public static class NpgsqlDataReaderExtensions
{
	public static async Task<T> Deserialize<T>(this NpgsqlDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		if (await reader.IsDBNullAsync(ordinal))
			return default!;

		var json = await reader.GetDataAsync<string>(columnName);
		return JsonSerializer.Deserialize<T>(json) ?? default!;
	}
	public static async Task<T> GetDataAsync<T>(this NpgsqlDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		return await reader.IsDBNullAsync(ordinal) ? default! : await reader.GetFieldValueAsync<T>(ordinal);
	}

	public static async Task<TimeSpan?> GetTimeOnly(this NpgsqlDataReader reader, string columnName)
	{
		var ordinal = reader.GetOrdinal(columnName);
		if (await reader.IsDBNullAsync(ordinal))
			return null;
		return await reader.GetDataAsync<TimeSpan>(columnName);
	}
}
