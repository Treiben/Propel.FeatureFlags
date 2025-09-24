using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Migrations;
using System.Data;

namespace Propel.FeatureFlags.Infrastructure.SqlServer;

public class SqlMigrationRepository : IMigrationRepository
{
	private readonly ILogger<SqlMigrationRepository> _logger;
	private readonly string _connectionString;
	private readonly string _masterConnectionString;
	private readonly string _schemaName;
	private readonly string _migrationTableName;
	private readonly int _timeoutSeconds;

	public string Database { get; }

	public SqlMigrationRepository(
		SqlMigrationOptions options,
		ILogger<SqlMigrationRepository> logger)
	{
		Database = options.Database;

		var connectionBuilder = new SqlConnectionStringBuilder(options.Connection)
		{
			InitialCatalog = Database
		};
		_connectionString = connectionBuilder.ConnectionString;
		_masterConnectionString = new SqlConnectionStringBuilder(options.Connection)
		{
			InitialCatalog = "master"
		}.ConnectionString;
		_schemaName = string.IsNullOrWhiteSpace(options.Schema) ? "dbo" : options.Schema;
		_migrationTableName = string.IsNullOrWhiteSpace(options.MigrationTable) ? "flags_schema_migrations" : options.MigrationTable;
		_timeoutSeconds = connectionBuilder.ConnectTimeout > 0 ? connectionBuilder.ConnectTimeout : 30;

		_logger = logger;
	}

	public async Task CreateDatabaseAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Creating database...");

		var sql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = '{Database}')
BEGIN
	CREATE DATABASE [{Database}]
END;";

		using var connection = new SqlConnection(_masterConnectionString);
		using var command = new SqlCommand(sql, connection);
		command.CommandTimeout = _timeoutSeconds;

		await connection.OpenAsync(cancellationToken);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task CreateSchemaAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Creating schema...");

		var sql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{_schemaName}')
BEGIN
    EXEC('CREATE SCHEMA [{_schemaName}] AUTHORIZATION [dbo]');
END;";

		using var connection = new SqlConnection(_connectionString);
		using var command = new SqlCommand(sql, connection);
		command.CommandTimeout = _timeoutSeconds;
		//execute query and if is sql exception for login failed then retry
		try
		{
			await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqlException ex) when (ex.Number == 18456 || ex.Number == 4060) // Login failed
		{
			_logger.LogWarning("Login failed when trying to create schema. Retrying in 5 seconds...");
			await Task.Delay(5000, cancellationToken); // wait for 5 seconds before retrying
			await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create schema");
			throw;
		}
	}

	public async Task CreateMigrationTableAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Creating migration tracking table...");

		var sql = $@"
IF (SELECT CASE WHEN EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_SCHEMA = '{_schemaName}' AND TABLE_NAME = '{_migrationTableName}'
) THEN 1 ELSE 0 END) = 0
BEGIN
	CREATE TABLE [{_schemaName}].[{_migrationTableName}] (
		version NVARCHAR(50) PRIMARY KEY,
		applied_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
		description NVARCHAR(MAX) NOT NULL)
END;";

		using var connection = new SqlConnection(_connectionString);
		using var command = new SqlCommand(sql, connection);
		command.CommandTimeout = _timeoutSeconds;

		await connection.OpenAsync(cancellationToken);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<List<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
	{
		var sql = $@"
                SELECT version 
                FROM [{_schemaName}].[{_migrationTableName}] 
                ORDER BY applied_at";

		using var connection = new SqlConnection(_connectionString);
		using var command = new SqlCommand(sql, connection);
		command.CommandTimeout = _timeoutSeconds;
		try
		{
			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			var migrations = new List<string>();
			while (await reader.ReadAsync(cancellationToken))
			{
				migrations.Add(reader.GetString("version"));
			}

			return migrations;
		}
		catch (SqlException ex) when (ex.Number == 208 || ex.Number == 4060) // Invalid object name
		{
			_logger.LogWarning("Database or migration table does not exist. Assuming no migrations have been applied yet.");
			return [];
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get applied migrations");
			return [];
		}
	}

	public async Task RecordMigrationAsync(string version, string description, CancellationToken cancellationToken = default)
	{
		var sql = $@"
                INSERT INTO [{_schemaName}].[{_migrationTableName}] (version, description, applied_at)
                VALUES (@version, @description, GETUTCDATE())";

		using var connection = new SqlConnection(_connectionString);
		using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@version", version);
		command.Parameters.AddWithValue("@description", description);
		command.CommandTimeout = _timeoutSeconds;

		await connection.OpenAsync(cancellationToken);
		await command.ExecuteNonQueryAsync(cancellationToken);

		_logger.LogDebug("Recorded migration: {Version}", version);
	}

	public async Task RemoveMigrationAsync(string version, CancellationToken cancellationToken = default)
	{
		var sql = $@"
                DELETE FROM [{_schemaName}].[{_migrationTableName}] 
                WHERE version = @version";

		using var connection = new SqlConnection(_connectionString);
		using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@version", version);
		command.CommandTimeout = _timeoutSeconds;

		await connection.OpenAsync(cancellationToken);
		await command.ExecuteNonQueryAsync(cancellationToken);

		_logger.LogDebug("Removed migration record: {Version}", version);
	}

	public async Task<bool> ExecuteSqlAsync(string sql, CancellationToken cancellationToken = default)
	{
		try
		{
			// Split SQL script by GO statements and execute each batch
			var batches = SplitSqlBatches(sql);

			using var connection = new SqlConnection(_connectionString);
			foreach (var batch in batches)
			{
				if (string.IsNullOrWhiteSpace(batch)) continue;

				_logger.LogDebug("Executing SQL batch: {Batch}", batch.Substring(0, Math.Min(100, batch.Length)));

				using var command = new SqlCommand(batch, connection);
				command.CommandTimeout = _timeoutSeconds;

				if (connection.State != ConnectionState.Open)
					await connection.OpenAsync(cancellationToken);
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
				if (currentBatch.Count != 0)
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
		if (currentBatch.Count != 0)
		{
			batches.Add(string.Join(Environment.NewLine, currentBatch));
		}

		return batches.Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
	}
}
