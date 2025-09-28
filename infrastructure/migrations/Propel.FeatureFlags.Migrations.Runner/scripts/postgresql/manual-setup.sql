-- =============================================================================
-- Propel Feature Flags - PostgreSQL Database Schema
-- Version: 1.0.0
-- =============================================================================

-- Variables (modify these before running)
-- Replace these values or use psql variables: psql -v dbname=your_db -v schema=your_schema -f script.sql
\set dbname 'propel_feature_flags'
\set schema 'public'
\set owner 'propel_user'

-- =============================================================================
-- 01_create_database.sql - Database Creation (Run as superuser)
-- =============================================================================

-- Check if database exists before creating
SELECT 'Database already exists: ' || :'dbname' 
WHERE EXISTS (SELECT 1 FROM pg_database WHERE datname = :'dbname');

-- Create database if it doesn't exist
-- Note: This requires superuser privileges or appropriate database creation rights
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'dbname') THEN
        PERFORM dblink_exec('dbname=postgres', 'CREATE DATABASE ' || quote_ident(:'dbname'));
        RAISE NOTICE 'Database % created successfully', :'dbname';
    ELSE
        RAISE NOTICE 'Database % already exists', :'dbname';
    END IF;
END
$$;

-- =============================================================================
-- 02_create_schema.sql - Schema and Tables Creation
-- =============================================================================

-- Connect to the target database first: \c :dbname

-- Create schema if it doesn't exist
CREATE SCHEMA IF NOT EXISTS :schema;

-- Set search path
SET search_path TO :schema;

-- Drop existing objects if they exist (for development/testing - comment out for production)
-- DROP TABLE IF EXISTS feature_flags_audit CASCADE;
-- DROP TABLE IF EXISTS feature_flags_metadata CASCADE;
-- DROP TABLE IF EXISTS feature_flags CASCADE;

-- =============================================================================
-- Core Tables
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

-- =============================================================================
-- Schema Version Tracking
-- =============================================================================

CREATE TABLE IF NOT EXISTS schema_migrations (
    version VARCHAR(50) PRIMARY KEY,
    applied_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    description TEXT NOT NULL
);

-- Record this migration
INSERT INTO schema_migrations (version, description) 
VALUES ('1.0.0', 'Initial feature flags schema creation')
ON CONFLICT (version) DO NOTHING;

-- =============================================================================
-- 03_create_indexes.sql - Performance Indexes
-- =============================================================================

-- Indexes for feature_flags table
CREATE INDEX IF NOT EXISTS ix_feature_flags_evaluation_modes 
    ON feature_flags USING GIN(evaluation_modes);

CREATE INDEX IF NOT EXISTS ix_feature_flags_scheduled_enable 
    ON feature_flags (scheduled_enable_date) 
    WHERE scheduled_enable_date IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_feature_flags_enabled_users 
    ON feature_flags USING GIN (enabled_users);

CREATE INDEX IF NOT EXISTS ix_feature_flags_enabled_tenants 
    ON feature_flags USING GIN (enabled_tenants);

CREATE INDEX IF NOT EXISTS ix_feature_flags_disabled_tenants 
    ON feature_flags USING GIN (disabled_tenants);

-- Read operation optimization indexes
CREATE INDEX IF NOT EXISTS ix_feature_flags_application_name 
    ON feature_flags (application_name);

CREATE INDEX IF NOT EXISTS ix_feature_flags_application_version 
    ON feature_flags (application_version);

CREATE INDEX IF NOT EXISTS ix_feature_flags_scope 
    ON feature_flags (scope);

-- Composite indexes for common query patterns
CREATE INDEX IF NOT EXISTS ix_feature_flags_scope_app_name 
    ON feature_flags (scope, application_name);

CREATE INDEX IF NOT EXISTS ix_feature_flags_app_version_scope 
    ON feature_flags (application_name, application_version, scope);

-- Audit table indexes
CREATE INDEX IF NOT EXISTS ix_feature_flags_audit_flag_key 
    ON feature_flags_audit (flag_key);

CREATE INDEX IF NOT EXISTS ix_feature_flags_audit_timestamp 
    ON feature_flags_audit (timestamp);

CREATE INDEX IF NOT EXISTS ix_feature_flags_audit_actor 
    ON feature_flags_audit (actor);

-- Metadata table indexes
CREATE INDEX IF NOT EXISTS ix_feature_flags_metadata_flag_key 
    ON feature_flags_metadata (flag_key);

CREATE INDEX IF NOT EXISTS ix_feature_flags_metadata_expiration 
    ON feature_flags_metadata (expiration_date) 
    WHERE is_permanent = FALSE;

-- =============================================================================
-- 05_grant_permissions.sql - Security Setup (modify as needed)
-- =============================================================================

-- Grant usage on schema
GRANT USAGE ON SCHEMA :schema TO :owner;

-- Grant table permissions
GRANT SELECT, INSERT, UPDATE ON feature_flags TO :owner;
GRANT SELECT, INSERT, UPDATE ON feature_flags_metadata TO :owner;
GRANT SELECT, INSERT ON feature_flags_audit TO :owner;
GRANT SELECT ON schema_migrations TO :owner;

-- Grant sequence permissions (for UUID generation)
GRANT USAGE ON ALL SEQUENCES IN SCHEMA :schema TO :owner;