using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Propel.FeatureFlags.Migrations.SqlServer;

public class SqlServerMigrationRepository : IMigrationRepository
{
	private readonly ILogger<SqlServerMigrationRepository> _logger;
	private readonly string _connectionString;
	private readonly string _masterConnectionString;
	private readonly string _databaseName;
	private readonly string _schemaName;
	private readonly string _migrationTableName;
	private readonly int _timeoutSeconds;

	public string DatabaseName => _databaseName;

	public SqlServerMigrationRepository(
		SqlMigrationOptions options,
		ILogger<SqlServerMigrationRepository> logger)
	{
		_logger = logger;

		_connectionString = options.Connection;

		var connectionBuilder = new SqlConnectionStringBuilder(_connectionString);
		_databaseName = connectionBuilder.InitialCatalog;
		_masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
		{
			InitialCatalog = "master"
		}.ConnectionString;
		_schemaName = string.IsNullOrWhiteSpace(options.Schema) ? "dbo" : options.Schema;
		_migrationTableName = string.IsNullOrWhiteSpace(options.MigrationTable) ? "flags_schema_migrations" : options.MigrationTable;
		_timeoutSeconds = connectionBuilder.ConnectTimeout > 0 ? connectionBuilder.ConnectTimeout : 30;
	}

	public async Task CreateDatabaseAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Creating database...");

		using var connection = new SqlConnection(_masterConnectionString);

		var sql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = '{_databaseName}')
BEGIN
	CREATE DATABASE [{_databaseName}]
END;";

		using var command = new SqlCommand(sql, connection);
		command.CommandTimeout = _timeoutSeconds;

		await connection.OpenAsync(cancellationToken);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task CreateSchemaAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Creating schema...");

		using var connection = new SqlConnection(_connectionString);

		var sql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{_schemaName}')
BEGIN
    EXEC('CREATE SCHEMA [{_schemaName}] AUTHORIZATION [dbo]');
END;";
		using var command = new SqlCommand(sql, connection);
		command.CommandTimeout = _timeoutSeconds;

		await connection.OpenAsync(cancellationToken);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task CreateMigrationTableAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Creating migration tracking table...");

		using var connection = new SqlConnection(_connectionString);

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

		using var command = new SqlCommand(sql, connection);
		command.CommandTimeout = _timeoutSeconds;

		await connection.OpenAsync(cancellationToken);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<List<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			using var connection = new SqlConnection(_connectionString);

			var sql = $@"
                SELECT version 
                FROM [{_schemaName}].[{_migrationTableName}] 
                ORDER BY applied_at";

			using var command = new SqlCommand(sql, connection);
			command.CommandTimeout = _timeoutSeconds;

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			var migrations = new List<string>();
			while (await reader.ReadAsync(cancellationToken))
			{
				migrations.Add(reader.GetString("version"));
			}

			return migrations;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get applied migrations");
			return [];
		}
	}

	public async Task RecordMigrationAsync(string version, string description, CancellationToken cancellationToken = default)
	{
		using var connection = new SqlConnection(_connectionString);

		var sql = $@"
                INSERT INTO [{_schemaName}].[{_migrationTableName}] (version, description, applied_at)
                VALUES (@version, @description, GETUTCDATE())";

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
		using var connection = new SqlConnection(_connectionString);

		var sql = $@"
                DELETE FROM [{_schemaName}].[{_migrationTableName}] 
                WHERE version = @version";

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
			using var connection = new SqlConnection(_connectionString);

			// Split SQL script by GO statements and execute each batch
			var batches = SplitSqlBatches(sql);

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
