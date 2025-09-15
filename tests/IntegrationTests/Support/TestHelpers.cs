using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Services.ApplicationScope;

namespace FeatureFlags.IntegrationTests.Support;

public class TestApplicationFeatureFlag : TypeSafeFeatureFlag
{
    public TestApplicationFeatureFlag(
        string key,
        string? name = null,
        string? description = null,
        Dictionary<string, string>? tags = null,
        EvaluationMode defaultMode = EvaluationMode.Disabled)
        : base(key, name, description, tags, defaultMode)
    {
    }
}
public static class TestHelpers
{
	private static readonly string _createTableSchema = GetCreateSchemaScript();
	public static async Task CreatePostgresTables(string connectionString)
	{
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand(_createTableSchema, connection);
		await command.ExecuteNonQueryAsync();
	}

	public static FeatureFlag CreateTestFlag(string key, EvaluationMode evaluationMode)
	{
		return new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for integration tests",
			ActiveEvaluationModes = new EvaluationModes([evaluationMode]),
			Created = new Audit(timestamp: DateTime.UtcNow, actor: "integration-test"),
		};
	}

    public static CacheKey CreateCacheKey(string key)
    {
		var applicationName = ApplicationInfo.Name;
		var applicationVersion = ApplicationInfo.Version;

		return new CacheKey(key, [applicationName, applicationVersion]);
	}

    public static CacheKey CreateGlobalCacheKey(string key)
    {
        return new CacheKey(key, ["global"]);
	}

	public static IApplicationFeatureFlag CreateApplicationFlag(string key, EvaluationMode evaluationMode)
    {
        return new TestApplicationFeatureFlag(
            key: key,
            name: $"App Flag {key}",
            description: "Application flag for integration tests",
            defaultMode: evaluationMode);
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

	-- Evaluation modes
    evaluation_modes JSONB NOT NULL DEFAULT '[]',

	-- Audit fields
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NULL,
    created_by VARCHAR(255) NOT NULL,
    updated_by VARCHAR(255) NULL,
    
    -- Retention
    is_permanent BOOLEAN NOT NULL DEFAULT FALSE,
    expiration_date TIMESTAMP WITH TIME ZONE NOT NULL,
	application_name VARCHAR(255) NOT NULL DEFAULT 'global',
	application_version VARCHAR(100) NULL,
	scope INT NOT NULL DEFAULT 0,
    
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
    user_percentage_enabled INTEGER NOT NULL DEFAULT 0 CHECK (user_percentage_enabled >= 0 AND user_percentage_enabled <= 100),

    -- Tenant-level controls
    enabled_tenants JSONB NOT NULL DEFAULT '[]',
    disabled_tenants JSONB NOT NULL DEFAULT '[]',
    tenant_percentage_enabled INTEGER NOT NULL DEFAULT 0 CHECK (tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100),
    
    -- Variations
    variations JSONB NOT NULL DEFAULT '{}',
    default_variation VARCHAR(255) NOT NULL DEFAULT 'off',
    
	-- Tags for categorization
    tags JSONB NOT NULL DEFAULT '{}'
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
