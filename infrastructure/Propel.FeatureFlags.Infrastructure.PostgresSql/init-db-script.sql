CREATE SCHEMA IF NOT EXISTS propel_test;

SET search_path TO propel_test;

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

	PRIMARY KEY (key, application_name, application_version, scope)
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
	reason TEXT NULL
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

-- Create indexes for feature_flag_audit table
CREATE INDEX IF NOT EXISTS idx_feature_flags_audit_flag_key ON feature_flags_audit (flag_key);