using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Persistence;
using System.Text.Json;

namespace Propel.FeatureFlags.SqlServer;

public class SqlServerFeatureFlagRepository : IFeatureFlagRepository
{
	private readonly string _connectionString;
	private readonly ILogger<SqlServerFeatureFlagRepository> _logger;

	public SqlServerFeatureFlagRepository(string connectionString, ILogger<SqlServerFeatureFlagRepository> logger)
	{
		_connectionString = connectionString;
		_logger = logger;
	}

	public async Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default)
	{

		_logger.LogDebug("Getting feature flag with key: {Key}", key);
		const string sql = @"
                SELECT [key], [name], [description], [status], created_at, updated_at, created_by, updated_by,
                       expiration_date, scheduled_enable_date, scheduled_disable_date,
                       window_start_time, window_end_time, time_zone, window_days,
                       percentage_enabled, targeting_rules, enabled_users, disabled_users,
					   enabled_tenants, disabled_tenants, tenant_percentage_enabled,
                       variations, default_variation, tags, is_permanent
                FROM feature_flags 
                WHERE [key] = @key";
		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("key", key);

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
                SELECT [key], [name], [description], [status], created_at, updated_at, created_by, updated_by,
                       expiration_date, scheduled_enable_date, scheduled_disable_date,
                       window_start_time, window_end_time, time_zone, window_days,
                       percentage_enabled, targeting_rules, enabled_users, disabled_users,
					   enabled_tenants, disabled_tenants, tenant_percentage_enabled,
                       variations, default_variation, tags, is_permanent
                FROM feature_flags 
                ORDER BY [name]";
		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			using var command = new SqlCommand(sql, connection);
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
                    [key], [name], [description], [status], created_at, updated_at, created_by, updated_by,
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
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			using var command = new SqlCommand(sql, connection);
			AddParameters(command, flag);

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
                    [name] = @name, [description] = @description, [status] = @status, updated_at = @updated_at, updated_by = @updated_by,
                    expiration_date = @expiration_date, scheduled_enable_date = @scheduled_enable_date, scheduled_disable_date = @scheduled_disable_date,
                    window_start_time = @window_start_time, window_end_time = @window_end_time, time_zone = @time_zone, window_days = @window_days,
                    percentage_enabled = @percentage_enabled, targeting_rules = @targeting_rules, enabled_users = @enabled_users, disabled_users = @disabled_users,
					enabled_tenants = @enabled_tenants, disabled_tenants = @disabled_tenants, tenant_percentage_enabled = @tenant_percentage_enabled,
                    variations = @variations, default_variation = @default_variation, tags = @tags, is_permanent = @is_permanent
                WHERE [key] = @key";

		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			using var command = new SqlCommand(sql, connection);
			AddParameters(command, flag);

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
		const string sql = "DELETE FROM feature_flags WHERE [key] = @key";

		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("key", key);

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
                SELECT [key], [name], [description], [status], created_at, updated_at, created_by, updated_by,
                       expiration_date, scheduled_enable_date, scheduled_disable_date,
                       window_start_time, window_end_time, time_zone, window_days,
                       percentage_enabled, targeting_rules, enabled_users, disabled_users,
					   enabled_tenants, disabled_tenants, tenant_percentage_enabled,
                       variations, default_variation, tags, is_permanent
                FROM feature_flags 
                WHERE expiration_date <= @before AND is_permanent = 0
                ORDER BY expiration_date";

		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("before", before);

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
		// Implementation for tag-based queries using SQL Server JSON functions
		var sql = @"SELECT [key], [name], [description], [status], created_at, updated_at, created_by, updated_by, 
			expiration_date, scheduled_enable_date, scheduled_disable_date, window_start_time, window_end_time, 
			time_zone, window_days, percentage_enabled, targeting_rules, 
			enabled_users, disabled_users, enabled_tenants, disabled_tenants, tenant_percentage_enabled,
			variations, default_variation, tags, is_permanent FROM feature_flags WHERE ";
		var conditions = tags.Select((_, i) => $"JSON_VALUE(tags, '$.{tags.ElementAt(i).Key}') = @tagValue{i}").ToList();
		sql += string.Join(" AND ", conditions);

		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			using var command = new SqlCommand(sql, connection);
			for (int i = 0; i < tags.Count; i++)
			{
				var tag = tags.ElementAt(i);
				command.Parameters.AddWithValue($"@tagValue{i}", tag.Value);
			}

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

	private static async Task<FeatureFlag> MapFromReader(SqlDataReader reader)
	{
		return new FeatureFlag
		{
			Key = reader.GetString(reader.GetOrdinal("key")),
			Name = reader.GetString(reader.GetOrdinal("name")),
			Description = reader.GetString(reader.GetOrdinal("description")),
			Status = (FeatureFlagStatus)reader.GetInt32(reader.GetOrdinal("status")),
			CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
			UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at")),
			CreatedBy = reader.GetString(reader.GetOrdinal("created_by")),
			UpdatedBy = reader.GetString(reader.GetOrdinal("updated_by")),
			ExpirationDate = await reader.GetNullableDateTimeAsync("expiration_date"),
			ScheduledEnableDate = await reader.GetNullableDateTimeAsync("scheduled_enable_date"),
			ScheduledDisableDate = await reader.GetNullableDateTimeAsync("scheduled_disable_date"),
			WindowStartTime = await reader.GetTimeOnly("window_start_time"),
			WindowEndTime = await reader.GetTimeOnly("window_end_time"),
			TimeZone = reader.IsDBNull(reader.GetOrdinal("time_zone")) ? null : reader.GetString(reader.GetOrdinal("time_zone")),
			WindowDays = await reader.Deserialize<List<DayOfWeek>>("window_days"),
			PercentageEnabled = reader.GetInt32(reader.GetOrdinal("percentage_enabled")),
			TargetingRules = await reader.Deserialize<List<TargetingRule>>("targeting_rules"),
			EnabledUsers = await reader.Deserialize<List<string>>("enabled_users"),
			DisabledUsers = await reader.Deserialize<List<string>>("disabled_users"),
			EnabledTenants = await reader.Deserialize<List<string>>("enabled_tenants"),
			DisabledTenants = await reader.Deserialize<List<string>>("disabled_tenants"),
			TenantPercentageEnabled = reader.GetInt32(reader.GetOrdinal("tenant_percentage_enabled")),
			Variations = await reader.Deserialize<Dictionary<string, object>>("variations"),
			DefaultVariation = reader.GetString(reader.GetOrdinal("default_variation")),
			Tags = await reader.Deserialize<Dictionary<string, string>>("tags"),
			IsPermanent = reader.GetBoolean(reader.GetOrdinal("is_permanent"))
		};
	}

	private static void AddParameters(SqlCommand command, FeatureFlag flag)
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
		command.Parameters.AddWithValue("window_days", JsonSerializer.Serialize(flag.WindowDays ?? new()));
		command.Parameters.AddWithValue("percentage_enabled", flag.PercentageEnabled);
		command.Parameters.AddWithValue("targeting_rules", JsonSerializer.Serialize(flag.TargetingRules));
		command.Parameters.AddWithValue("enabled_users", JsonSerializer.Serialize(flag.EnabledUsers));
		command.Parameters.AddWithValue("disabled_users", JsonSerializer.Serialize(flag.DisabledUsers));
		command.Parameters.AddWithValue("enabled_tenants", JsonSerializer.Serialize(flag.EnabledTenants));
		command.Parameters.AddWithValue("disabled_tenants", JsonSerializer.Serialize(flag.DisabledTenants));
		command.Parameters.AddWithValue("tenant_percentage_enabled", flag.TenantPercentageEnabled);
		command.Parameters.AddWithValue("variations", JsonSerializer.Serialize(flag.Variations));
		command.Parameters.AddWithValue("default_variation", flag.DefaultVariation);
		command.Parameters.AddWithValue("tags", JsonSerializer.Serialize(flag.Tags));
		command.Parameters.AddWithValue("is_permanent", flag.IsPermanent);
	}
}

public static class SqlDataReaderExtensions
{
	public static async Task<T> Deserialize<T>(this SqlDataReader reader, string columnName)
	{
		int ordinal = reader.GetOrdinal(columnName);
		if (reader.IsDBNull(ordinal))
			return default!;

		var json = reader.GetString(ordinal);
		return JsonSerializer.Deserialize<T>(json) ?? default!;
	}

	public static async Task<DateTime?> GetNullableDateTimeAsync(this SqlDataReader reader, string columnName)
	{
		int ordinal = reader.GetOrdinal(columnName);
		return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
	}

	public static async Task<TimeSpan?> GetTimeOnly(this SqlDataReader reader, string columnName)
	{
		int ordinal = reader.GetOrdinal(columnName);
		return reader.IsDBNull(ordinal) ? null : reader.GetTimeSpan(ordinal);
	}
}
