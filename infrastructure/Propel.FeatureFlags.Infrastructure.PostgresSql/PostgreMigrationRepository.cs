using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Migrations;
using System.Data;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql;

public class PostgreMigrationRepository : IMigrationRepository
{
	private readonly ILogger<PostgreMigrationRepository> _logger;
	private readonly string _connectionString;
	private readonly string _masterConnectionString;
	private readonly string _schemaName;
	private readonly string _migrationTableName;
	private readonly int _timeoutSeconds;

	public string Database { get; }

	public PostgreMigrationRepository(
		SqlMigrationOptions options,
		ILogger<PostgreMigrationRepository> logger)
	{
		Database = options.Database;

		var connectionBuilder = new NpgsqlConnectionStringBuilder(options.Connection)
		{
			Database = Database
		};
		_connectionString = connectionBuilder.ConnectionString;
		_masterConnectionString = new NpgsqlConnectionStringBuilder(options.Connection)
		{
			Database = "postgres"
		}.ConnectionString;
		_schemaName = string.IsNullOrWhiteSpace(options.Schema) ? "public" : options.Schema;
		_migrationTableName = string.IsNullOrWhiteSpace(options.MigrationTable) ? "flags_schema_migrations" : options.MigrationTable;
		_timeoutSeconds = connectionBuilder.Timeout > 0 ? connectionBuilder.Timeout : 30;

		_logger = logger;

	}

	public async Task CreateDatabaseAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Ensuring database exists...");

		using var connection = new NpgsqlConnection(_masterConnectionString);
		using var command = new NpgsqlCommand
		{
			Connection = connection,
			CommandTimeout = _timeoutSeconds
		};
		command.CommandText = $"SELECT 1 FROM pg_database WHERE datname = @databaseName";
		command.Parameters.AddWithValue("databaseName", Database);
		try
		{
			await connection.OpenAsync(cancellationToken);

			// Check if the database exists
			var exists = await command.ExecuteScalarAsync(cancellationToken) != null;

			if (!exists)
			{
				_logger.LogInformation($"Database '{Database}' not found. Creating it now...");

				command.CommandText = $"CREATE DATABASE \"{Database}\"";
				command.Parameters.Clear(); 

				await command.ExecuteNonQueryAsync(cancellationToken);
				_logger.LogInformation($"Database '{Database}' created successfully.");
			}
			else
			{
				_logger.LogInformation($"Database '{Database}' already exists.");
			}
		}
		catch (PostgresException ex) when (ex.SqlState == "42P04")
		{
			// This handles a race condition where another process
			// creates the database after your check but before your CREATE command.
			_logger.LogWarning($"Database '{Database}' was created by another process. Continuing.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create or verify database.");
			throw;
		}
	}


	public async Task CreateSchemaAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Creating schema...");

		var sql = $@"CREATE SCHEMA IF NOT EXISTS ""{_schemaName}"";";

		using var connection = new NpgsqlConnection(_connectionString);
		using var command = new NpgsqlCommand(sql, connection);
		command.CommandTimeout = _timeoutSeconds;

		await connection.OpenAsync(cancellationToken);
		await command.ExecuteNonQueryAsync(cancellationToken);

		_logger.LogInformation($"Schema '{_schemaName}' created successfully.");
	}

	public async Task CreateMigrationTableAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Creating migration tracking table...");

		var sql = $@"
CREATE TABLE IF NOT EXISTS ""{_schemaName}"".""{_migrationTableName}"" (
    version VARCHAR(50) PRIMARY KEY,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    description TEXT NOT NULL
);";

		using var connection = new NpgsqlConnection(_connectionString);
		using var command = new NpgsqlCommand(sql, connection);
		command.CommandTimeout = _timeoutSeconds;

		await connection.OpenAsync(cancellationToken);
		await command.ExecuteNonQueryAsync(cancellationToken);

		_logger.LogInformation($"Migration tracking table '{_migrationTableName}' created successfully.");
	}

	public async Task<List<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
	{
		var sql = $@"
                SELECT version 
                FROM ""{_schemaName}"".""{_migrationTableName}""
                ORDER BY applied_at";

		using var connection = new NpgsqlConnection(_connectionString);
		using var command = new NpgsqlCommand(sql, connection);
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
		// catch exception database or table or schema does not exist 		
		catch (PostgresException ex) when (ex.SqlState == "3D000" || ex.SqlState == "42P01" || ex.SqlState == "3F000")
		{
			_logger.LogWarning("Database, schema, or migration table does not exist. Returning empty migration list.");
			return [];
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to retrieve applied migrations");
			throw;
		}
	}

	public async Task RecordMigrationAsync(string version, string description, CancellationToken cancellationToken = default)
	{
		var sql = $@"
                INSERT INTO ""{_schemaName}"".""{_migrationTableName}"" (version, description, applied_at)
                VALUES (@version, @description, NOW())";

		using var connection = new NpgsqlConnection(_connectionString);
		using var command = new NpgsqlCommand(sql, connection);
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
                DELETE FROM ""{_schemaName}"".""{_migrationTableName}""
                WHERE version = @version";

		using var connection = new NpgsqlConnection(_connectionString);
		using var command = new NpgsqlCommand(sql, connection);
		command.Parameters.AddWithValue("@version", version);
		command.CommandTimeout = _timeoutSeconds;

		try
		{
			await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);

			_logger.LogDebug("Removed migration record: {Version}", version);
		}
		// Catch exception if table or schema does not exist
		catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "3F000")
		{
			_logger.LogWarning("Schema or migration table does not exist or already removed.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to remove migration record");
			throw;
		}
	}

	public async Task<bool> ExecuteSqlAsync(string sql, CancellationToken cancellationToken = default)
	{
		// Split SQL script by GO statements and execute each batch
		var batches = SplitSqlBatches(sql);
		using var connection = new NpgsqlConnection(_connectionString);
		try
		{
			foreach (var batch in batches)
			{
				if (string.IsNullOrWhiteSpace(batch)) continue;

				_logger.LogDebug("Executing SQL batch: {Batch}", batch.Substring(0, Math.Min(100, batch.Length)));

				using var command = new NpgsqlCommand(batch, connection);
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
