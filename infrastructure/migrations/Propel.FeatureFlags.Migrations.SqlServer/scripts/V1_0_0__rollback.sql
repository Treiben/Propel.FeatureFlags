-- =============================================================================
-- Scripts/Migrations/Rollback/V1_0_0__initial_schema.sql
-- =============================================================================

-- WARNING: This will destroy all feature flag data!
-- Only use in development or with proper backups

-- Drop tables in reverse dependency order
-- DROP TABLE IF EXISTS FeatureFlagsAudit
-- DROP TABLE IF EXISTS FeatureFlagsMetadata
-- DROP TABLE IF EXISTS FeatureFlags

PRINT 'Schema rollback completed - all tables dropped'