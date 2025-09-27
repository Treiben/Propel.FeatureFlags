using Microsoft.Extensions.Logging;
using Npgsql;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql;

public class PostgresDatabaseInitializer
{
	private readonly string _connectionString;
	private readonly string _masterConnectionString;
	private readonly string _databaseName;
	private readonly string _searchPath;
	private readonly ILogger<PostgresDatabaseInitializer> _logger;

	public PostgresDatabaseInitializer(string connectionString, ILogger<PostgresDatabaseInitializer> logger)
	{
		ArgumentException.ThrowIfNullOrEmpty(connectionString, nameof(connectionString));

		_connectionString = connectionString;
		_logger = logger;

		var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
		_databaseName = connectionBuilder.Database!;
		_searchPath = connectionBuilder.SearchPath ?? "public";

		// Connect to postgres database to create the target database
		connectionBuilder.Database = "postgres";
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
		using var connection = new NpgsqlConnection(_connectionString);
		using var command = new NpgsqlCommand(script, connection);

		await connection.OpenAsync();
		await command.ExecuteNonQueryAsync();

		_logger.LogInformation("Successfully seeded database from script file {File}", sqlScript);

		return true;
	}

	private async Task<bool> DatabaseExistsAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new NpgsqlConnection(_masterConnectionString);

		var checkDbSql = "SELECT 1 FROM pg_database WHERE datname = @dbName";
		using var checkCmd = new NpgsqlCommand(checkDbSql, connection);
		checkCmd.Parameters.AddWithValue("dbName", _databaseName);

		await connection.OpenAsync(cancellationToken);

		return await checkCmd.ExecuteScalarAsync(cancellationToken) != null;
	}

	private async Task<bool> DataExistsAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new NpgsqlConnection(_connectionString);

		var checkDataSql = "SELECT 1 FROM feature_flags LIMIT 1";
		using var checkCmd = new NpgsqlCommand(checkDataSql, connection);

		await connection.OpenAsync(cancellationToken);

		return await checkCmd.ExecuteScalarAsync(cancellationToken) != null;
	}

	private async Task<bool> CreateDatabaseAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new NpgsqlConnection(_masterConnectionString);

		var createDbSql = $"CREATE DATABASE \"{_databaseName}\"";
		using var createCmd = new NpgsqlCommand(createDbSql, connection);

		await connection.OpenAsync(cancellationToken);

		await createCmd.ExecuteNonQueryAsync(cancellationToken);

		_logger.LogInformation("Successfully created database {DatabaseName}", _databaseName);
		return true;
	}

	private async Task<bool> SchemaExistsAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new NpgsqlConnection(_connectionString);

		var checkTableSql = @"
				SELECT EXISTS (
					SELECT FROM information_schema.tables 
					WHERE table_name = 'feature_flags')";
		using var checkCmd = new NpgsqlCommand(checkTableSql, connection);

		await connection.OpenAsync(cancellationToken);

		return (bool)(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? false);
	}

	private async Task<bool> CreateSchemaAsync(CancellationToken cancellationToken = default)
	{
		var createSchemaSql = GetCreateSchemaScript();

		using var connection = new NpgsqlConnection(_connectionString);
		using var createCmd = new NpgsqlCommand(createSchemaSql, connection);

		await connection.OpenAsync(cancellationToken);

		await createCmd.ExecuteNonQueryAsync(cancellationToken);

		_logger.LogInformation("Successfully created feature flags schema");
		return true;
	}

	private string GetCreateSchemaScript()
	{
		var schema = $"CREATE SCHEMA IF NOT EXISTS {_searchPath};";
		return schema + @"
-- Create the feature_flags table
CREATE TABLE feature_flags (
	-- Flag uniquness scope
    key VARCHAR(255) NOT NULL,
	application_name VARCHAR(255) NOT NULL DEFAULT 'global',
	application_version VARCHAR(100) NOT NULL DEFAULT '0.0.0.0',
	scope INT NOT NULL DEFAULT 0,
	
	-- Descriptive fields
    name VARCHAR(500) NOT NULL,
    description TEXT NOT NULL DEFAULT '',

	-- Evaluation modes
    evaluation_modes JSONB NOT NULL DEFAULT '[]',
    
    -- Scheduling
    scheduled_enable_date TIMESTAMP WITH TIME ZONE NULL,
    scheduled_disable_date TIMESTAMP WITH TIME ZONE NULL,
    
    -- Time Windows
    window_start_time TIME NULL,
    window_end_time TIME NULL,
    time_zone VARCHAR(100) NULL,
    window_days JSONB NOT NULL DEFAULT '[]',
    
    -- Targeting
    targeting_rules JSONB NOT NULL DEFAULT '[]',

	-- User-level controls
    enabled_users JSONB NOT NULL DEFAULT '[]',
    disabled_users JSONB NOT NULL DEFAULT '[]',
    user_percentage_enabled INTEGER NOT NULL DEFAULT 100 CHECK (user_percentage_enabled >= 0 AND user_percentage_enabled <= 100),

    -- Tenant-level controls
    enabled_tenants JSONB NOT NULL DEFAULT '[]',
    disabled_tenants JSONB NOT NULL DEFAULT '[]',
    tenant_percentage_enabled INTEGER NOT NULL DEFAULT 100 CHECK (tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100),
    
    -- Variations
    variations JSONB NOT NULL DEFAULT '{}',
    default_variation VARCHAR(255) NOT NULL DEFAULT 'off',

	PRIMARY KEY (key, application_name, application_version)
);

-- Create the metdata table
CREATE TABLE feature_flags_metadata (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
	flag_key VARCHAR(255) NOT NULL,

	-- Flag uniquness scope
	application_name VARCHAR(255) NOT NULL DEFAULT('global'),
	application_version VARCHAR(100) NOT NULL DEFAULT('0.0.0.0'),

    -- Retention and expiration
    is_permanent BOOLEAN NOT NULL DEFAULT FALSE,
    expiration_date TIMESTAMP WITH TIME ZONE NOT NULL,

	-- Tags for categorization
    tags JSONB NOT NULL DEFAULT '{}'
);

-- Create the feature_flags_audit table
CREATE TABLE feature_flags_audit (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
	flag_key VARCHAR(255) NOT NULL,

	-- Flag uniquness scope
	application_name VARCHAR(255) NULL DEFAULT 'global',
	application_version VARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

	-- Action details
	action VARCHAR(50) NOT NULL,
	actor VARCHAR(255) NOT NULL,
	timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
	notes TEXT NULL
);

-- Create indexes for feature_flags table
CREATE INDEX IF NOT EXISTS ix_feature_flags_evaluation_modes ON feature_flags USING GIN(evaluation_modes);
CREATE INDEX IF NOT EXISTS idx_feature_flags_scheduled_enable ON feature_flags (scheduled_enable_date) WHERE scheduled_enable_date IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_feature_flags_enabled_users ON feature_flags USING GIN (enabled_users);
CREATE INDEX IF NOT EXISTS idx_feature_flags_enabled_tenants ON feature_flags USING GIN (enabled_tenants);
CREATE INDEX IF NOT EXISTS idx_feature_flags_disabled_tenants ON feature_flags USING GIN (disabled_tenants);

-- Add indexes for read operation optimization (application_name, application_version, scope)
CREATE INDEX IF NOT EXISTS idx_feature_flags_application_name ON feature_flags (application_name);
CREATE INDEX IF NOT EXISTS idx_feature_flags_application_version ON feature_flags (application_version);
CREATE INDEX IF NOT EXISTS idx_feature_flags_scope ON feature_flags (scope);

-- Composite index for filtering operations from BuildFilterConditions method
CREATE INDEX IF NOT EXISTS idx_feature_flags_scope_app_name ON feature_flags (scope, application_name);
";
	}
}
