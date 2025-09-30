-- =============================================================================
-- Initial schema for Feature Flags system
-- Database: PostgreSQL
-- Scripts/Migrations/V1_0_0__initial_schema.sql
-- =============================================================================

-- Create the feature_flags table
CREATE TABLE IF NOT EXISTS feature_flags (
    -- Flag uniqueness scope
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
    user_percentage_enabled INTEGER NOT NULL DEFAULT 100 
        CHECK (user_percentage_enabled >= 0 AND user_percentage_enabled <= 100),

    -- Tenant-level controls
    enabled_tenants JSONB NOT NULL DEFAULT '[]',
    disabled_tenants JSONB NOT NULL DEFAULT '[]',
    tenant_percentage_enabled INTEGER NOT NULL DEFAULT 100 
        CHECK (tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100),
    
    -- Variations
    variations JSONB NOT NULL DEFAULT '{}',
    default_variation VARCHAR(255) NOT NULL DEFAULT 'off',

    CONSTRAINT pk_feature_flags PRIMARY KEY (key, application_name, application_version)
);

-- Create the metadata table
CREATE TABLE IF NOT EXISTS feature_flags_metadata (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    flag_key VARCHAR(255) NOT NULL,

    -- Flag uniqueness scope
    application_name VARCHAR(255) NOT NULL DEFAULT 'global',
    application_version VARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

    -- Retention and expiration
    is_permanent BOOLEAN NOT NULL DEFAULT FALSE,
    expiration_date TIMESTAMP WITH TIME ZONE NOT NULL,

    -- Tags for categorization
    tags JSONB NOT NULL DEFAULT '{}'
);

-- Create the feature_flags_audit table
CREATE TABLE IF NOT EXISTS feature_flags_audit (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    flag_key VARCHAR(255) NOT NULL,

    -- Flag uniqueness scope
    application_name VARCHAR(255) NULL DEFAULT 'global',
    application_version VARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

    -- Action details
    action VARCHAR(50) NOT NULL,
    actor VARCHAR(255) NOT NULL,
    timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    notes TEXT NULL
);