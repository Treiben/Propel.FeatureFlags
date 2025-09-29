using DbMigrationCli.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace Propel.FeatureFlags.Migrations.CLI.Providers;

public class PostgreSqlProvider : IDatabaseProvider
{
    private readonly ILogger<PostgreSqlProvider> _logger;
    private string _schema = "public";

	public PostgreSqlProvider(ILogger<PostgreSqlProvider> logger)
    {
        _logger = logger;
    }

    public async Task<bool> DatabaseExistsAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        builder.Database = "postgres";

        using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        var sql = "SELECT COUNT(*) FROM pg_database WHERE datname = @DatabaseName";
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DatabaseName", databaseName);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task CreateDatabaseAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        builder.Database = "postgres";

        using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        var sql = $"CREATE DATABASE \"{databaseName}\"";
        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();

        _logger.LogInformation("Database {DatabaseName} created successfully", databaseName);

		// After creating the database, create the schema if it doesn't exist
	    _logger.LogInformation("Creating schema...");
        await CreateSchemaIfNotExistsAsync(connectionString);
	}

	private async Task CreateSchemaIfNotExistsAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
		_schema = string.IsNullOrWhiteSpace(builder.SearchPath) ? "public" : builder.SearchPath;

		_logger.LogInformation("Creating schema...");

		var sql = $@"CREATE SCHEMA IF NOT EXISTS ""{_schema}"";";

		using var connection = new NpgsqlConnection(connectionString);
		using var command = new NpgsqlCommand(sql, connection);

		await connection.OpenAsync();
		await command.ExecuteNonQueryAsync();

		_logger.LogInformation($"Schema '{_schema}' created successfully.");
	}

	public async Task<bool> MigrationTableExistsAsync(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = $@"
            SELECT COUNT(*) 
            FROM information_schema.tables 
            WHERE table_schema = '{_schema}' AND table_name = '__migrationhistory'";

        using var command = new NpgsqlCommand(sql, connection);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task CreateMigrationTableAsync(string connectionString)
    {
		var builder = new NpgsqlConnectionStringBuilder(connectionString);
		var schema = string.IsNullOrWhiteSpace(builder.SearchPath) ? "public" : builder.SearchPath;

		using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = GetCreateMigrationTableSql(schema);
        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();

        _logger.LogInformation("Migration history table created successfully");
    }

    public async Task<IEnumerable<AppliedMigration>> GetAppliedMigrationsAsync(string connectionString)
    {
		var builder = new NpgsqlConnectionStringBuilder(connectionString);
		var schema = string.IsNullOrWhiteSpace(builder.SearchPath) ? "public" : builder.SearchPath;
		
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = GetSelectMigrationsSql(schema);
        using var command = new NpgsqlCommand(sql, connection);

        var migrations = new List<AppliedMigration>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            migrations.Add(new AppliedMigration
            {
                Version = reader.GetString("version"),
                Description = reader.GetString("description"),
                AppliedAt = reader.GetDateTime("applied_at")
            });
        }

        return migrations;
    }

    public async Task AddMigrationRecordAsync(string connectionString, string version, string description)
    {
		var builder = new NpgsqlConnectionStringBuilder(connectionString);
		var schema = string.IsNullOrWhiteSpace(builder.SearchPath) ? "public" : builder.SearchPath;

		using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = GetInsertMigrationSql(schema);
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@version", version);
        command.Parameters.AddWithValue("@description", description);
        command.Parameters.AddWithValue("@applied_at", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveMigrationRecordAsync(string connectionString, string version)
    {
		var builder = new NpgsqlConnectionStringBuilder(connectionString);
		var schema = string.IsNullOrWhiteSpace(builder.SearchPath) ? "public" : builder.SearchPath;
		
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = GetDeleteMigrationSql(schema);
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@version", version);

        await command.ExecuteNonQueryAsync();
    }

    public async Task ExecuteSqlAsync(string connectionString, string sql)
    {
		var builder = new NpgsqlConnectionStringBuilder(connectionString);
		var schema = string.IsNullOrWhiteSpace(builder.SearchPath) ? "public" : builder.SearchPath;
		
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // PostgreSQL doesn't use GO statements, execute as single batch
        sql = $@"SET search_path TO {schema}; {sql}";
		using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 300; // 5 minutes timeout
        await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result, typeof(T));
    }

	private string GetCreateMigrationTableSql(string? schema = null)
    {
		if (!string.IsNullOrWhiteSpace(schema))
		{
			schema = "public";
		}
		
        return $@"
            CREATE TABLE {schema}.__migrationhistory (
                version VARCHAR(50) NOT NULL PRIMARY KEY,
                description VARCHAR(500) NOT NULL,
                applied_at TIMESTAMP NOT NULL
            )";
    }

	private string GetInsertMigrationSql(string? schema = null)
    {
		if (!string.IsNullOrWhiteSpace(schema))
		{
			schema = "public";
		}
		
        return $@"
            INSERT INTO {schema}.__migrationhistory (version, description, applied_at)
            VALUES (@version, @description, @applied_at)";
    }

	private string GetDeleteMigrationSql(string? schema = null)
    {
		if (!string.IsNullOrWhiteSpace(schema))
		{
			schema = "public";
		}
		
        return $@"DELETE FROM {schema}.__migrationhistory WHERE version = @version";
    }

    private string GetSelectMigrationsSql(string? schema = null)
    {
        if (!string.IsNullOrWhiteSpace(schema))
        {
			schema = "public";
		}
		return $@"
            SELECT version, description, applied_at 
            FROM {schema}.__migrationhistory 
            ORDER BY version";
    }
}