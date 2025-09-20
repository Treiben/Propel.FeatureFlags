// =============================================================================
// Services/SqlServerMigrationRepository.cs - SQL Server Implementation
// =============================================================================
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Propel.FeatureFlags.Migrations.SqlServer;

public class SqlServerMigrationRepository : IMigrationRepository
{
	private readonly ILogger<SqlServerMigrationRepository> _logger;
	private readonly IConfiguration _configuration;
	private readonly string _connectionString;
	private readonly string _masterConnectionString;
	private readonly string _databaseName;
	private readonly string _schemaName;
	private readonly string _migrationTableName;
	private readonly int _timeoutSeconds;

	public SqlServerMigrationRepository(
		ILogger<SqlServerMigrationRepository> logger,
		IConfiguration configuration)
	{
		_logger = logger;
		_configuration = configuration;

		_connectionString = _configuration.GetConnectionString("DefaultConnection")
			?? throw new InvalidOperationException("DefaultConnection connection string is required");
		_masterConnectionString = _configuration.GetConnectionString("MasterConnection")
			?? throw new InvalidOperationException("MasterConnection connection string is required");

		_databaseName = _configuration.GetValue<string>("Migration:DatabaseName") ?? "PropelFeatureFlags";
		_schemaName = _configuration.GetValue<string>("Migration:SchemaName") ?? "dbo";
		_migrationTableName = _configuration.GetValue<string>("Migration:MigrationTableName") ?? "schema_migrations";
		_timeoutSeconds = _configuration.GetValue("Migration:TimeoutSeconds", 30);
	}

	public async Task<bool> DatabaseExistsAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			using var connection = new SqlConnection(_masterConnectionString);
			await connection.OpenAsync(cancellationToken);

			var sql = "SELECT 1 FROM sys.databases WHERE name = @DatabaseName";
			using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("@DatabaseName", _databaseName);
			command.CommandTimeout = _timeoutSeconds;

			var result = await command.ExecuteScalarAsync(cancellationToken);
			return result != null;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check if database exists: {DatabaseName}", _databaseName);
			throw;
		}
	}

	public async Task CreateDatabaseAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			using var connection = new SqlConnection(_masterConnectionString);
			await connection.OpenAsync(cancellationToken);

			var sql = $"CREATE DATABASE [{_databaseName}]";
			using var command = new SqlCommand(sql, connection);
			command.CommandTimeout = _timeoutSeconds;

			await command.ExecuteNonQueryAsync(cancellationToken);
			_logger.LogInformation("Database {DatabaseName} created successfully", _databaseName);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create database: {DatabaseName}", _databaseName);
			throw;
		}
	}

	public async Task<bool> MigrationTableExistsAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			var sql = @"
                SELECT CASE WHEN EXISTS (
                    SELECT * FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_SCHEMA = @SchemaName 
                    AND TABLE_NAME = @TableName
                ) THEN 1 ELSE 0 END";

			using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("@SchemaName", _schemaName);
			command.Parameters.AddWithValue("@TableName", _migrationTableName);
			command.CommandTimeout = _timeoutSeconds;

			var result = await command.ExecuteScalarAsync(cancellationToken);
			return Convert.ToBoolean(result);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check if migration table exists");
			throw;
		}
	}

	public async Task CreateMigrationTableAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			var sql = $@"
                CREATE TABLE [{_schemaName}].[{_migrationTableName}] (
                    version NVARCHAR(50) PRIMARY KEY,
                    applied_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
                    description NVARCHAR(MAX) NOT NULL
                )";

			using var command = new SqlCommand(sql, connection);
			command.CommandTimeout = _timeoutSeconds;

			await command.ExecuteNonQueryAsync(cancellationToken);
			_logger.LogInformation("Migration table {TableName} created successfully", _migrationTableName);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create migration table: {TableName}", _migrationTableName);
			throw;
		}
	}

	public async Task<List<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			var sql = $@"
                SELECT version 
                FROM [{_schemaName}].[{_migrationTableName}] 
                ORDER BY applied_at";

			using var command = new SqlCommand(sql, connection);
			command.CommandTimeout = _timeoutSeconds;

			var migrations = new List<string>();
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			while (await reader.ReadAsync(cancellationToken))
			{
				migrations.Add(reader.GetString("version"));
			}

			return migrations;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get applied migrations");
			throw;
		}
	}

	public async Task RecordMigrationAsync(string version, string description, CancellationToken cancellationToken = default)
	{
		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			var sql = $@"
                INSERT INTO [{_schemaName}].[{_migrationTableName}] (version, description, applied_at)
                VALUES (@Version, @Description, GETUTCDATE())";

			using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("@Version", version);
			command.Parameters.AddWithValue("@Description", description);
			command.CommandTimeout = _timeoutSeconds;

			await command.ExecuteNonQueryAsync(cancellationToken);
			_logger.LogDebug("Recorded migration: {Version}", version);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to record migration: {Version}", version);
			throw;
		}
	}

	public async Task RemoveMigrationAsync(string version, CancellationToken cancellationToken = default)
	{
		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			var sql = $@"
                DELETE FROM [{_schemaName}].[{_migrationTableName}] 
                WHERE version = @Version";

			using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("@Version", version);
			command.CommandTimeout = _timeoutSeconds;

			await command.ExecuteNonQueryAsync(cancellationToken);
			_logger.LogDebug("Removed migration record: {Version}", version);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to remove migration: {Version}", version);
			throw;
		}
	}

	public async Task<bool> ExecuteSqlAsync(string sql, CancellationToken cancellationToken = default)
	{
		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			// Split SQL script by GO statements and execute each batch
			var batches = SplitSqlBatches(sql);

			foreach (var batch in batches)
			{
				if (string.IsNullOrWhiteSpace(batch)) continue;

				_logger.LogDebug("Executing SQL batch: {Batch}", batch.Substring(0, Math.Min(100, batch.Length)));

				using var command = new SqlCommand(batch, connection);
				command.CommandTimeout = _timeoutSeconds;
				await command.ExecuteNonQueryAsync(cancellationToken);
			}

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to execute SQL script");
			return false;
		}
	}

	private static List<string> SplitSqlBatches(string sql)
	{
		// Split by GO statements (case insensitive, on separate lines)
		var batches = new List<string>();
		var lines = sql.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
		var currentBatch = new List<string>();

		foreach (var line in lines)
		{
			var trimmedLine = line.Trim();

			if (string.Equals(trimmedLine, "GO", StringComparison.OrdinalIgnoreCase))
			{
				if (currentBatch.Any())
				{
					batches.Add(string.Join(Environment.NewLine, currentBatch));
					currentBatch.Clear();
				}
			}
			else
			{
				currentBatch.Add(line);
			}
		}

		// Add the final batch if it exists
		if (currentBatch.Any())
		{
			batches.Add(string.Join(Environment.NewLine, currentBatch));
		}

		return batches.Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
	}
}
