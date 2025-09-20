using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace Propel.FeatureFlags.Infrastructure.SqlServer;

public class SqlServerDatabaseInitializer
{
	private readonly string _connectionString;
	private readonly string _masterConnectionString;
	private readonly string _databaseName;
	private readonly ILogger<SqlServerDatabaseInitializer> _logger;

	public SqlServerDatabaseInitializer(string connectionString, ILogger<SqlServerDatabaseInitializer> logger)
	{
		ArgumentException.ThrowIfNullOrEmpty(connectionString, nameof(connectionString));

		_connectionString = connectionString;
		_logger = logger;

		var connectionBuilder = new SqlConnectionStringBuilder(connectionString);
		_databaseName = connectionBuilder.InitialCatalog;

		// Connect to master database to create the target database
		connectionBuilder.InitialCatalog = "master";
		_masterConnectionString = connectionBuilder.ToString();
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

	public async Task<bool> SeedAsync(string sqlScript)
	{
		if (!File.Exists(sqlScript))
		{
			_logger.LogWarning("SQL script file {File} does not exist. Skipping seeding.", sqlScript);
			return false;
		}
		var script = await File.ReadAllTextAsync(sqlScript);
		if (string.IsNullOrWhiteSpace(script))
		{
			_logger.LogWarning("SQL script file {File} is empty. Skipping seeding.", sqlScript);
			return false;
		}

		var dataExists = await DataExistsAsync();
		if (dataExists)
		{
			_logger.LogInformation("Database already contains data. Skipping seeding from script file {File}", sqlScript);
			return true;
		}

		using var connection = new SqlConnection(_connectionString);
		using var command = new SqlCommand(script, connection);

		await connection.OpenAsync();
		await command.ExecuteNonQueryAsync();

		_logger.LogInformation("Successfully seeded database from script file {File}", sqlScript);

		return true;
	}

	private async Task<bool> DatabaseExistsAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new SqlConnection(_masterConnectionString);

		var checkDbSql = "SELECT 1 FROM sys.databases WHERE name = @dbName";
		using var checkCmd = new SqlCommand(checkDbSql, connection);
		checkCmd.Parameters.AddWithValue("@dbName", _databaseName);

		await connection.OpenAsync(cancellationToken);

		return await checkCmd.ExecuteScalarAsync(cancellationToken) != null;
	}

	private async Task<bool> DataExistsAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new SqlConnection(_connectionString);

		var checkDataSql = "SELECT TOP 1 1 FROM feature_flags";
		using var checkCmd = new SqlCommand(checkDataSql, connection);

		await connection.OpenAsync(cancellationToken);

		return await checkCmd.ExecuteScalarAsync(cancellationToken) != null;
	}

	private async Task<bool> CreateDatabaseAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new SqlConnection(_masterConnectionString);

		var createDbSql = $"CREATE DATABASE [{_databaseName}]";
		using var createCmd = new SqlCommand(createDbSql, connection);

		await connection.OpenAsync(cancellationToken);

		await createCmd.ExecuteNonQueryAsync(cancellationToken);

		_logger.LogInformation("Successfully created database {DatabaseName}", _databaseName);
		return true;
	}

	private async Task<bool> SchemaExistsAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new SqlConnection(_connectionString);

		var checkTableSql = @"
				SELECT CASE WHEN EXISTS (
					SELECT * FROM INFORMATION_SCHEMA.TABLES 
					WHERE TABLE_NAME = 'feature_flags')
				THEN 1 ELSE 0 END";
		using var checkCmd = new SqlCommand(checkTableSql, connection);

		await connection.OpenAsync(cancellationToken);

		return Convert.ToBoolean(await checkCmd.ExecuteScalarAsync(cancellationToken));
	}

	private async Task<bool> CreateSchemaAsync(CancellationToken cancellationToken = default)
	{
		var createSchemaSql = GetCreateSchemaScript();

		using var connection = new SqlConnection(_connectionString);
		using var createCmd = new SqlCommand(createSchemaSql, connection);

		await connection.OpenAsync(cancellationToken);

		await createCmd.ExecuteNonQueryAsync(cancellationToken);

		_logger.LogInformation("Successfully created feature flags schema");
		return true;
	}

	private static string GetCreateSchemaScript()
	{
		return @"
-- Create the feature_flags table
CREATE TABLE feature_flags (
	-- Flag uniqueness scope
    [key] NVARCHAR(255) NOT NULL,
	application_name NVARCHAR(255) NOT NULL DEFAULT 'global',
	application_version NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',
	scope INT NOT NULL DEFAULT 0,
	
	-- Descriptive fields
    name NVARCHAR(500) NOT NULL,
    description NVARCHAR(MAX) NOT NULL DEFAULT '',

	-- Evaluation modes
    evaluation_modes NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_evaluation_modes_json CHECK (ISJSON(evaluation_modes) = 1),
    
    -- Scheduling
    scheduled_enable_date DATETIMEOFFSET NULL,
    scheduled_disable_date DATETIMEOFFSET NULL,
    
    -- Time Windows
    window_start_time TIME NULL,
    window_end_time TIME NULL,
    time_zone NVARCHAR(100) NULL,
    window_days NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_window_days_json CHECK (ISJSON(window_days) = 1),
    
    -- Targeting
    targeting_rules NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_targeting_rules_json CHECK (ISJSON(targeting_rules) = 1),

	-- User-level controls
    enabled_users NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_enabled_users_json CHECK (ISJSON(enabled_users) = 1),
    disabled_users NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_disabled_users_json CHECK (ISJSON(disabled_users) = 1),
    user_percentage_enabled INT NOT NULL DEFAULT 100 
        CONSTRAINT CK_user_percentage CHECK (user_percentage_enabled >= 0 AND user_percentage_enabled <= 100),

    -- Tenant-level controls
    enabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_enabled_tenants_json CHECK (ISJSON(enabled_tenants) = 1),
    disabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_disabled_tenants_json CHECK (ISJSON(disabled_tenants) = 1),
    tenant_percentage_enabled INT NOT NULL DEFAULT 100 
        CONSTRAINT CK_tenant_percentage CHECK (tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100),
    
    -- Variations
    variations NVARCHAR(MAX) NOT NULL DEFAULT '{}'
        CONSTRAINT CK_variations_json CHECK (ISJSON(variations) = 1),
    default_variation NVARCHAR(255) NOT NULL DEFAULT 'off',

	CONSTRAINT PK_feature_flags PRIMARY KEY ([key], application_name, application_version, scope)
);

-- Create the metadata table
CREATE TABLE feature_flags_metadata (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
	flag_key NVARCHAR(255) NOT NULL,

	-- Flag uniqueness scope
	application_name NVARCHAR(255) NOT NULL DEFAULT 'global',
	application_version NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

    -- Retention and expiration
    is_permanent BIT NOT NULL DEFAULT 0,
    expiration_date DATETIMEOFFSET NOT NULL,

	-- Tags for categorization
    tags NVARCHAR(MAX) NOT NULL DEFAULT '{}'
        CONSTRAINT CK_metadata_tags_json CHECK (ISJSON(tags) = 1)
);

-- Create the feature_flags_audit table
CREATE TABLE feature_flags_audit (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
	flag_key NVARCHAR(255) NOT NULL,

	-- Flag uniqueness scope
	application_name NVARCHAR(255) NULL DEFAULT 'global',
	application_version NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

	-- Action details
	action NVARCHAR(50) NOT NULL,
	actor NVARCHAR(255) NOT NULL,
	timestamp DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
	reason NVARCHAR(MAX) NULL
);

-- Create indexes for feature_flags table
CREATE NONCLUSTERED INDEX IX_feature_flags_scheduled_enable 
    ON feature_flags (scheduled_enable_date) 
    WHERE scheduled_enable_date IS NOT NULL;

-- Add indexes for read operation optimization
CREATE NONCLUSTERED INDEX IX_feature_flags_application_name ON feature_flags (application_name);
CREATE NONCLUSTERED INDEX IX_feature_flags_application_version ON feature_flags (application_version);
CREATE NONCLUSTERED INDEX IX_feature_flags_scope ON feature_flags (scope);

-- Composite index for filtering operations
CREATE NONCLUSTERED INDEX IX_feature_flags_scope_app_name ON feature_flags (scope, application_name);
";
	}
}