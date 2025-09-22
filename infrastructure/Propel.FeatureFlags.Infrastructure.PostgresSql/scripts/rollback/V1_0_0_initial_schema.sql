-- =============================================================================
-- Initial schema rollback for Feature Flags system
-- Database: PostgreSQL
-- Rollback Script (rollback.sql)
-- =============================================================================


-- WARNING: This will destroy all feature flag data!
-- Only use in development or with proper backups

-- Drop triggers first
DROP TRIGGER IF EXISTS trigger_feature_flags_updated_at ON feature_flags;
DROP TRIGGER IF EXISTS trigger_feature_flags_metadata_updated_at ON feature_flags_metadata;

-- Drop function
DROP FUNCTION IF EXISTS update_updated_at_column();

-- Drop tables in reverse dependency order
DROP TABLE IF EXISTS feature_flags_audit CASCADE;
DROP TABLE IF EXISTS feature_flags_metadata CASCADE; 
DROP TABLE IF EXISTS feature_flags CASCADE;
DROP TABLE IF EXISTS schema_migrations CASCADE;

-- Optionally drop schema (if it was created specifically for this)
-- DROP SCHEMA IF EXISTS :schema CASCADE;

PRINT 'Schema rollback completed - all tables dropped'
