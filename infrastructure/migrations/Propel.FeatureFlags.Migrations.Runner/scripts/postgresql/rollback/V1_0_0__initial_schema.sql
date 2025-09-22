-- =============================================================================
-- Initial schema rollback for Feature Flags system
-- Database: PostgreSQL
-- Rollback Script (rollback.sql)
-- =============================================================================


-- WARNING: This will destroy all feature flag data!
-- Only use in development or with proper backups

-- Drop tables in reverse dependency order
DROP TABLE IF EXISTS feature_flags_audit CASCADE;
DROP TABLE IF EXISTS feature_flags_metadata CASCADE; 
DROP TABLE IF EXISTS feature_flags CASCADE;

-- Optionally drop schema (if it was created specifically for this)
-- DROP SCHEMA IF EXISTS :schema CASCADE;
