using Microsoft.Extensions.Logging;
using Npgsql;

namespace Propel.FeatureFlags.PostgresSql;

public class PostgreSQLDatabaseInitializer
{
	private readonly string _connectionString;
	private readonly string _masterConnectionString;
	private readonly string _databaseName;
	private readonly ILogger<PostgreSQLDatabaseInitializer> _logger;

	public PostgreSQLDatabaseInitializer(string connectionString, ILogger<PostgreSQLDatabaseInitializer> logger)
	{
		ArgumentException.ThrowIfNullOrEmpty(connectionString, nameof(connectionString));

		this._connectionString = connectionString;
		this._logger = logger;

		var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
		_databaseName = connectionBuilder.Database!;

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

	private async Task<bool> DatabaseExistsAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new NpgsqlConnection(_masterConnectionString);

		var checkDbSql = "SELECT 1 FROM pg_database WHERE datname = @dbName";
		using var checkCmd = new NpgsqlCommand(checkDbSql, connection);
		checkCmd.Parameters.AddWithValue("dbName", _databaseName);

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
		using var connection = new NpgsqlConnection(_connectionString);

		var createSchemaSql = GetCreateSchemaScript();
		using var createCmd = new NpgsqlCommand(createSchemaSql, connection);

		await connection.OpenAsync(cancellationToken);

		await createCmd.ExecuteNonQueryAsync(cancellationToken);

		_logger.LogInformation("Successfully created feature flags schema");
		return true;
	}

	private static string GetCreateSchemaScript()
	{
		return @"
-- Create UUID extension for the audit table
CREATE EXTENSION IF NOT EXISTS ""uuid-ossp"";

-- Create the feature_flags table
CREATE TABLE feature_flags (
    key VARCHAR(255) PRIMARY KEY,
    name VARCHAR(500) NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    evaluation_modes JSONB NOT NULL DEFAULT '[]',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NULL,
    created_by VARCHAR(255) NOT NULL,
    updated_by VARCHAR(255) NOT NULL,
    
    -- Expiration
    expiration_date TIMESTAMP WITH TIME ZONE NOT NULL,
    
    -- Scheduling
    scheduled_enable_date TIMESTAMP WITH TIME ZONE NULL,
    scheduled_disable_date TIMESTAMP WITH TIME ZONE NULL,
    
    -- Time Windows
    window_start_time TIME NULL,
    window_end_time TIME NULL,
    time_zone VARCHAR(100) NULL,
    window_days JSONB NOT NULL DEFAULT '[]',
    
    -- Percentage rollout
    percentage_enabled INTEGER NOT NULL DEFAULT 0 CHECK (percentage_enabled >= 0 AND percentage_enabled <= 100),
    
    -- Targeting
    targeting_rules JSONB NOT NULL DEFAULT '[]',
    enabled_users JSONB NOT NULL DEFAULT '[]',
    disabled_users JSONB NOT NULL DEFAULT '[]',
    
    -- Tenant-level controls
    enabled_tenants JSONB NOT NULL DEFAULT '[]',
    disabled_tenants JSONB NOT NULL DEFAULT '[]',
    tenant_percentage_enabled INTEGER NOT NULL DEFAULT 0 CHECK (tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100),
    
    -- Variations
    variations JSONB NOT NULL DEFAULT '{}',
    default_variation VARCHAR(255) NOT NULL DEFAULT 'off',
    
    -- Metadata
    tags JSONB NOT NULL DEFAULT '{}',
    is_permanent BOOLEAN NOT NULL DEFAULT FALSE
);

-- Create the feature_flag_audit table
CREATE TABLE feature_flag_audit (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    flag_key VARCHAR(255) NOT NULL,
    action VARCHAR(50) NOT NULL,
    changed_by VARCHAR(255) NOT NULL,
    changed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    old_values JSONB NULL,
    new_values JSONB NULL,
    reason TEXT NULL
);

-- Create indexes for feature_flags table
CREATE INDEX IF NOT EXISTS ix_feature_flags_evaluation_modes ON feature_flags USING GIN(evaluation_modes);
CREATE INDEX IF NOT EXISTS idx_feature_flags_created_at ON feature_flags (created_at);
CREATE INDEX IF NOT EXISTS idx_feature_flags_updated_at ON feature_flags (updated_at);
CREATE INDEX IF NOT EXISTS idx_feature_flags_expiration_date ON feature_flags (expiration_date) WHERE expiration_date IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_feature_flags_scheduled_enable ON feature_flags (scheduled_enable_date) WHERE scheduled_enable_date IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_feature_flags_tags ON feature_flags USING GIN (tags);
CREATE INDEX IF NOT EXISTS idx_feature_flags_enabled_users ON feature_flags USING GIN (enabled_users);
CREATE INDEX IF NOT EXISTS idx_feature_flags_enabled_tenants ON feature_flags USING GIN (enabled_tenants);
CREATE INDEX IF NOT EXISTS idx_feature_flags_disabled_tenants ON feature_flags USING GIN (disabled_tenants);

-- Create indexes for feature_flag_audit table
CREATE INDEX IF NOT EXISTS idx_feature_flag_audit_flag_key ON feature_flag_audit (flag_key);
CREATE INDEX IF NOT EXISTS idx_feature_flag_audit_changed_at ON feature_flag_audit (changed_at);
CREATE INDEX IF NOT EXISTS idx_feature_flag_audit_changed_by ON feature_flag_audit (changed_by);

-- Function to automatically update the updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Trigger to automatically update updated_at
DROP TRIGGER IF EXISTS update_feature_flags_updated_at ON feature_flags;
CREATE TRIGGER update_feature_flags_updated_at 
    BEFORE UPDATE ON feature_flags 
    FOR EACH ROW 
    EXECUTE FUNCTION update_updated_at_column();
";
	}
}
