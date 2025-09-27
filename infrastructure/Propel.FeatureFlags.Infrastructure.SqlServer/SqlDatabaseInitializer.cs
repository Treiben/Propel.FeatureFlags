using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace Propel.FeatureFlags.Infrastructure.SqlServer;

public class SqlDatabaseInitializer
{
	private readonly string _connectionString;
	private readonly string _masterConnectionString;
	private readonly string _databaseName;
	private readonly ILogger<SqlDatabaseInitializer> _logger;

	public SqlDatabaseInitializer(string connectionString, ILogger<SqlDatabaseInitializer> logger)
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

		var checkDataSql = "SELECT TOP 1 1 FROM FeatureFlags";
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
					WHERE TABLE_NAME = 'FeatureFlags')
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
-- Create the FeatureFlags table
CREATE TABLE FeatureFlags (
	-- Flag uniqueness scope
    [Key] NVARCHAR(255) NOT NULL,
	ApplicationName NVARCHAR(255) NOT NULL DEFAULT 'global',
	ApplicationVersion NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',
	Scope INT NOT NULL DEFAULT 0,
	
	-- Descriptive fields
    Name NVARCHAR(500) NOT NULL,
    Description NVARCHAR(MAX) NOT NULL DEFAULT '',

	-- Evaluation modes
    EvaluationModes NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_EvaluationModes_json CHECK (ISJSON(EvaluationModes) = 1),
    
    -- Scheduling
    ScheduledEnableDate DATETIMEOFFSET NULL,
    ScheduledDisableDate DATETIMEOFFSET NULL,
    
    -- Time Windows
    WindowStartTime TIME NULL,
    WindowEndTime TIME NULL,
    TimeZone NVARCHAR(100) NULL,
    WindowDays NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_WindowDays_json CHECK (ISJSON(WindowDays) = 1),
    
    -- Targeting
    TargetingRules NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_TargetingRules_json CHECK (ISJSON(TargetingRules) = 1),

	-- User-level controls
    EnabledUsers NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_EnabledUsers CHECK (ISJSON(EnabledUsers) = 1),
    DisabledUsers NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_DisabledUsers CHECK (ISJSON(DisabledUsers) = 1),
    UserPercentageEnabled INT NOT NULL DEFAULT 100 
        CONSTRAINT CK_UserPercentageEnabled CHECK (UserPercentageEnabled >= 0 AND UserPercentageEnabled <= 100),

    -- Tenant-level controls
    EnabledTenants NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_EnabledTenants_json CHECK (ISJSON(EnabledTenants) = 1),
    DisabledTenants NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_DisabledTenants_json CHECK (ISJSON(DisabledTenants) = 1),
    TenantPercentageEnabled INT NOT NULL DEFAULT 100 
        CONSTRAINT CK_TenantPercentageEnabled CHECK (TenantPercentageEnabled >= 0 AND TenantPercentageEnabled <= 100),
    
    -- Variations
    Variations NVARCHAR(MAX) NOT NULL DEFAULT '{}'
        CONSTRAINT CK_Variations_json CHECK (ISJSON(variations) = 1),
    DefaultVariation NVARCHAR(255) NOT NULL DEFAULT 'off',

	CONSTRAINT PK_feature_flags PRIMARY KEY ([key], ApplicationName, ApplicationVersion)
);

-- Create the metadata table
CREATE TABLE FeatureFlagsMetadata (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
	FlagKey NVARCHAR(255) NOT NULL,

	-- Flag uniqueness scope
	ApplicationName NVARCHAR(255) NOT NULL DEFAULT 'global',
	ApplicationVersion NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

    -- Retention and expiration
    IsPermanent BIT NOT NULL DEFAULT 0,
    ExpirationDate DATETIMEOFFSET NOT NULL,

	-- Tags for categorization
    Tags NVARCHAR(MAX) NOT NULL DEFAULT '{}'
        CONSTRAINT CK_metadata_tags_json CHECK (ISJSON(tags) = 1)
);

-- Create the feature_flags_audit table
CREATE TABLE FeatureFlagsAudit (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
	FlagKey NVARCHAR(255) NOT NULL,

	-- Flag uniqueness scope
	ApplicationName NVARCHAR(255) NULL DEFAULT 'global',
	ApplicationVersion NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

	-- Action details
	Action NVARCHAR(50) NOT NULL,
	Actor NVARCHAR(255) NOT NULL,
	Timestamp DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
	Notes NVARCHAR(MAX) NULL
);";
	}
}