using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Propel.FeatureFlags.SqlServer;

public class SqlServerDatabaseInitializer
{
	private readonly string _connectionString;
	private readonly string _masterConnectionString;
	private readonly string _databaseName;
	private readonly ILogger<SqlServerDatabaseInitializer> _logger;

	public SqlServerDatabaseInitializer(string connectionString, ILogger<SqlServerDatabaseInitializer> logger)
	{
		ArgumentException.ThrowIfNullOrEmpty(connectionString, nameof(connectionString));

		this._connectionString = connectionString;
		this._logger = logger;

		var connectionBuilder = new SqlConnectionStringBuilder(connectionString);
		_databaseName = connectionBuilder.InitialCatalog!;

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

	public async Task<bool> DatabaseExistsAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new SqlConnection(_masterConnectionString);

		var checkDbSql = "SELECT 1 FROM sys.databases WHERE name = @dbName";
		using var checkCmd = new SqlCommand(checkDbSql, connection);
		checkCmd.Parameters.AddWithValue("@dbName", _databaseName);

		await connection.OpenAsync(cancellationToken);

		return await checkCmd.ExecuteScalarAsync(cancellationToken) != null;
	}

	public async Task<bool> CreateDatabaseAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new SqlConnection(_masterConnectionString);

		// Create database
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
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_NAME = 'feature_flags'";

		using var checkCmd = new SqlCommand(checkTableSql, connection);

		await connection.OpenAsync(cancellationToken);

		return Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;
	}

	public async Task<bool> CreateSchemaAsync(CancellationToken cancellationToken = default)
	{
		using var connection = new SqlConnection(_connectionString);

		var createSchemaSql = GetCreateSchemaScript();
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
    [key] NVARCHAR(255) PRIMARY KEY,
    [name] NVARCHAR(500) NOT NULL,
    [description] NVARCHAR(MAX) NOT NULL DEFAULT '',
    [status] INT NOT NULL DEFAULT 0,
    created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    updated_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    created_by NVARCHAR(255) NOT NULL,
    updated_by NVARCHAR(255) NOT NULL,
    
    -- Expiration
    expiration_date DATETIME2 NULL,
    
    -- Scheduling
    scheduled_enable_date DATETIME2 NULL,
    scheduled_disable_date DATETIME2 NULL,
    
    -- Time Windows
    window_start_time TIME NULL,
    window_end_time TIME NULL,
    time_zone NVARCHAR(100) NULL,
    window_days NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    
    -- Percentage rollout
    percentage_enabled INT NOT NULL DEFAULT 0 CHECK (percentage_enabled >= 0 AND percentage_enabled <= 100),
    
    -- Targeting
    targeting_rules NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    enabled_users NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    disabled_users NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    
    -- Tenant-level controls
    enabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    disabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    tenant_percentage_enabled INT NOT NULL DEFAULT 0 CHECK (tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100),
    
    -- Variations
    variations NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    default_variation NVARCHAR(255) NOT NULL DEFAULT 'off',
    
    -- Metadata
    tags NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    is_permanent BIT NOT NULL DEFAULT 0
);

-- Create the feature_flag_audit table
CREATE TABLE feature_flag_audit (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    flag_key NVARCHAR(255) NOT NULL,
    action NVARCHAR(50) NOT NULL,
    changed_by NVARCHAR(255) NOT NULL,
    changed_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    old_values NVARCHAR(MAX) NULL,
    new_values NVARCHAR(MAX) NULL,
    reason NVARCHAR(MAX) NULL
);

-- Create indexes for feature_flags table
CREATE INDEX IX_feature_flags_status ON feature_flags([status]);
CREATE INDEX IX_feature_flags_created_at ON feature_flags(created_at);
CREATE INDEX IX_feature_flags_updated_at ON feature_flags(updated_at);
CREATE INDEX IX_feature_flags_expiration_date ON feature_flags(expiration_date) WHERE expiration_date IS NOT NULL;
CREATE INDEX IX_feature_flags_scheduled_enable ON feature_flags(scheduled_enable_date) WHERE scheduled_enable_date IS NOT NULL;

-- Create indexes for feature_flag_audit table
CREATE INDEX IX_feature_flag_audit_flag_key ON feature_flag_audit(flag_key);
CREATE INDEX IX_feature_flag_audit_changed_at ON feature_flag_audit(changed_at);
CREATE INDEX IX_feature_flag_audit_changed_by ON feature_flag_audit(changed_by);
";
	}
}