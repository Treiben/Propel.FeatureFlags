using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite
{
    internal sealed class SqliteDatabaseInitializer
    {
        private readonly string _connectionString;
        private readonly string _databasePath;
        private readonly ILogger<SqliteDatabaseInitializer> _logger;

        public SqliteDatabaseInitializer(string connectionString, ILogger<SqliteDatabaseInitializer> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger;

            // Extract database file path from connection string
            var builder = new SqliteConnectionStringBuilder(connectionString);
            _databasePath = builder.DataSource;
        }

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            var databaseExists = await DatabaseExistsAsync(cancellationToken);
            if (!databaseExists && !await CreateDatabaseAsync(cancellationToken))
            {
                return false;
            }

            var schemaExists = await SchemaExistsAsync(cancellationToken);
            if (!schemaExists && !await CreateSchemaAsync(cancellationToken))
            {
                return false;
            }

            return true;
        }

        private Task<bool> DatabaseExistsAsync(CancellationToken cancellationToken = default)
        {
            // SQLite databases are just files
            var exists = File.Exists(_databasePath);
            return Task.FromResult(exists);
        }

        private async Task<bool> CreateDatabaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_databasePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Opening a connection to a non-existent SQLite database creates it
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                _logger.LogInformation("Successfully created SQLite database at {DatabasePath}", _databasePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create SQLite database at {DatabasePath}", _databasePath);
                return false;
            }
        }

        private async Task<bool> SchemaExistsAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);

            // SQLite uses sqlite_master instead of INFORMATION_SCHEMA
            var checkTableSql = @"
                SELECT COUNT(*) 
                FROM sqlite_master 
                WHERE type='table' AND name='FeatureFlags'";
            
            using var checkCmd = new SqliteCommand(checkTableSql, connection);

            await connection.OpenAsync(cancellationToken);

            var result = await checkCmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) > 0;
        }

        private async Task<bool> CreateSchemaAsync(CancellationToken cancellationToken = default)
        {
            var createSchemaSql = GetCreateSchemaScript();

            using var connection = new SqliteConnection(_connectionString);
            using var createCmd = new SqliteCommand(createSchemaSql, connection);

            await connection.OpenAsync(cancellationToken);

            await createCmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Successfully created feature flags schema in SQLite database");
            return true;
        }

        private static string GetCreateSchemaScript()
        {
            // SQLite-compatible schema (no NVARCHAR, no DATETIMEOFFSET, no CHECK constraints with ISJSON)
            return @"
-- Create the FeatureFlags table
CREATE TABLE FeatureFlags (
    -- Flag uniqueness scope
    Key TEXT NOT NULL,
    ApplicationName TEXT NOT NULL DEFAULT 'global',
    ApplicationVersion TEXT NOT NULL DEFAULT '0.0.0.0',
    Scope INTEGER NOT NULL DEFAULT 0,
    
    -- Descriptive fields
    Name TEXT NOT NULL,
    Description TEXT NOT NULL DEFAULT '',

    -- Evaluation modes (JSON stored as TEXT)
    EvaluationModes TEXT NOT NULL DEFAULT '[]',
    
    -- Scheduling
    ScheduledEnableDate TEXT NULL,
    ScheduledDisableDate TEXT NULL,
    
    -- Time Windows
    WindowStartTime TEXT NULL,
    WindowEndTime TEXT NULL,
    TimeZone TEXT NULL,
    WindowDays TEXT NOT NULL DEFAULT '[]',
    
    -- Targeting
    TargetingRules TEXT NOT NULL DEFAULT '[]',

    -- User-level controls
    EnabledUsers TEXT NOT NULL DEFAULT '[]',
    DisabledUsers TEXT NOT NULL DEFAULT '[]',
    UserPercentageEnabled INTEGER NOT NULL DEFAULT 100 CHECK (UserPercentageEnabled >= 0 AND UserPercentageEnabled <= 100),

    -- Tenant-level controls
    EnabledTenants TEXT NOT NULL DEFAULT '[]',
    DisabledTenants TEXT NOT NULL DEFAULT '[]',
    TenantPercentageEnabled INTEGER NOT NULL DEFAULT 100 CHECK (TenantPercentageEnabled >= 0 AND TenantPercentageEnabled <= 100),
    
    -- Variations
    Variations TEXT NOT NULL DEFAULT '{}',
    DefaultVariation TEXT NOT NULL DEFAULT '',

    PRIMARY KEY (Key, ApplicationName, ApplicationVersion)
);

-- Create the metadata table
CREATE TABLE FeatureFlagsMetadata (
    Id TEXT PRIMARY KEY NOT NULL,
    FlagKey TEXT NOT NULL,

    -- Flag uniqueness scope
    ApplicationName TEXT NOT NULL DEFAULT 'global',
    ApplicationVersion TEXT NOT NULL DEFAULT '0.0.0.0',

    -- Retention and expiration
    IsPermanent INTEGER NOT NULL DEFAULT 0,
    ExpirationDate TEXT NOT NULL,

    -- Tags for categorization
    Tags TEXT NOT NULL DEFAULT '{}'
);

-- Create the feature_flags_audit table
CREATE TABLE FeatureFlagsAudit (
    Id TEXT PRIMARY KEY NOT NULL,
    FlagKey TEXT NOT NULL,

    -- Flag uniqueness scope
    ApplicationName TEXT NULL DEFAULT 'global',
    ApplicationVersion TEXT NOT NULL DEFAULT '0.0.0.0',

    -- Action details
    Action TEXT NOT NULL,
    Actor TEXT NOT NULL,
    Timestamp TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Notes TEXT NULL
);";
        }
    }
}