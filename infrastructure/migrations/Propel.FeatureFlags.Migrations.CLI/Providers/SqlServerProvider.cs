using System.Data;
using DbMigrationCli.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Propel.FeatureFlags.Migrations.CLI.Providers;

public class SqlServerProvider : IDatabaseProvider
{
    private readonly ILogger<SqlServerProvider> _logger;

	public SqlServerProvider(ILogger<SqlServerProvider> logger)
    {
        _logger = logger;
    }

    public async Task<bool> DatabaseExistsAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        var sql = "SELECT COUNT(*) FROM sys.databases WHERE name = @DatabaseName";
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DatabaseName", databaseName);

        var count = (int)await command.ExecuteScalarAsync();
        return count > 0;
    }

    public async Task CreateDatabaseAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        var sql = $"CREATE DATABASE [{databaseName}]";
        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();

        _logger.LogInformation("Database {DatabaseName} created successfully", databaseName);
    }

	public async Task<bool> MigrationTableExistsAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = '__MigrationHistory'";

        using var command = new SqlCommand(sql, connection);
        var count = (int)await command.ExecuteScalarAsync();
        return count > 0;
    }

    public async Task CreateMigrationTableAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = GetCreateMigrationTableSql();
        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();

        _logger.LogInformation("Migration history table created successfully");
    }

    public async Task<IEnumerable<AppliedMigration>> GetAppliedMigrationsAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = GetSelectMigrationsSql();
        using var command = new SqlCommand(sql, connection);

        var migrations = new List<AppliedMigration>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            migrations.Add(new AppliedMigration
            {
                Version = reader.GetString("Version"),
                Description = reader.GetString("Description"),
                AppliedAt = reader.GetDateTime("AppliedAt")
            });
        }

        return migrations;
    }

    public async Task AddMigrationRecordAsync(string connectionString, string version, string description)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = GetInsertMigrationSql();
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@Description", description);
        command.Parameters.AddWithValue("@AppliedAt", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveMigrationRecordAsync(string connectionString, string version)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = GetDeleteMigrationSql();
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Version", version);

        await command.ExecuteNonQueryAsync();
    }

    public async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Split SQL by GO statements for SQL Server
        var batches = sql.Split(new[] { "\nGO\n", "\nGO\r\n", "\rGO\r", "\ngo\n" }, 
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var batch in batches)
        {
            var trimmedBatch = batch.Trim();
            if (string.IsNullOrEmpty(trimmedBatch)) continue;

            using var command = new SqlCommand(trimmedBatch, connection);
            command.CommandTimeout = 300; // 5 minutes timeout
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result, typeof(T));
    }

	private string GetCreateMigrationTableSql()
    {
        return @"
            CREATE TABLE [dbo].[__MigrationHistory] (
                [Version] NVARCHAR(50) NOT NULL PRIMARY KEY,
                [Description] NVARCHAR(500) NOT NULL,
                [AppliedAt] DATETIME2 NOT NULL
            )";
    }

	private string GetInsertMigrationSql()
    {
        return @"
            INSERT INTO [dbo].[__MigrationHistory] ([Version], [Description], [AppliedAt])
            VALUES (@Version, @Description, @AppliedAt)";
    }

	private string GetDeleteMigrationSql()
    {
        return "DELETE FROM [dbo].[__MigrationHistory] WHERE [Version] = @Version";
    }

    private string GetSelectMigrationsSql()
    {
        return @"
            SELECT [Version], [Description], [AppliedAt] 
            FROM [dbo].[__MigrationHistory] 
            ORDER BY [Version]";
    }
}