using Microsoft.Data.Sqlite;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite
{
	internal sealed class SqliteDatabaseInitializer
	{
		private readonly SqliteConnection _inMemoryConnection;

		public SqliteDatabaseInitializer(SqliteConnection inMemoryConnection)
		{
			_inMemoryConnection = inMemoryConnection ?? throw new ArgumentNullException(nameof(inMemoryConnection));
		}

		public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
		{
			// For in-memory databases, we don't check if database exists
			// It's created when the connection is opened
			
			var schemaExists = await SchemaExistsAsync(cancellationToken);
			if (!schemaExists)
			{
				return await CreateSchemaAsync(cancellationToken);
			}

			return true;
		}

		private async Task<bool> SchemaExistsAsync(CancellationToken cancellationToken = default)
		{
			var checkTableSql = @"
				SELECT COUNT(*) 
				FROM sqlite_master 
				WHERE type='table' AND name='FeatureFlags'";
			
			using (var checkCmd = new SqliteCommand(checkTableSql, _inMemoryConnection))
			{
				var result = await checkCmd.ExecuteScalarAsync(cancellationToken);
				return Convert.ToInt32(result) > 0;
			}
		}

		private async Task<bool> CreateSchemaAsync(CancellationToken cancellationToken = default)
		{
			try
			{
				var createSchemaSql = GetCreateSchemaScript();

				using (var createCmd = new SqliteCommand(createSchemaSql, _inMemoryConnection))
				{
					await createCmd.ExecuteNonQueryAsync(cancellationToken);
					return true;
				}
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Failed to create SQLite schema for feature flags", ex);
			}
		}

		private static string GetCreateSchemaScript()
		{
			// SQLite-compatible schema for in-memory database
			return @"
-- Create the FeatureFlags table
CREATE TABLE IF NOT EXISTS FeatureFlags (
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
CREATE TABLE IF NOT EXISTS FeatureFlagsMetadata (
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
CREATE TABLE IF NOT EXISTS FeatureFlagsAudit (
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