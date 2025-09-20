-- =============================================================================
-- Propel Feature Flags - SQL Server Database Schema
-- Version: 1.0.0
-- =============================================================================

-- Variables (modify these before running)
DECLARE @DatabaseName NVARCHAR(128) = N'PropelFeatureFlags'
DECLARE @SchemaName NVARCHAR(128) = N'dbo'
DECLARE @UserName NVARCHAR(128) = N'propel_user'

-- =============================================================================
-- 01_create_database.sql - Database Creation (Run as sysadmin)
-- =============================================================================

-- Check if database exists
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = @DatabaseName)
BEGIN
    DECLARE @CreateDbSql NVARCHAR(MAX) = N'CREATE DATABASE [' + @DatabaseName + N']'
    EXEC sp_executesql @CreateDbSql
    PRINT 'Database ' + @DatabaseName + ' created successfully'
END
ELSE
BEGIN
    PRINT 'Database ' + @DatabaseName + ' already exists'
END

-- Switch to the target database
DECLARE @UseSql NVARCHAR(MAX) = N'USE [' + @DatabaseName + N']'
EXEC sp_executesql @UseSql

-- =============================================================================
-- 02_create_schema.sql - Schema and Tables Creation
-- =============================================================================

-- Create custom schema if not using dbo
IF @SchemaName != N'dbo' AND NOT EXISTS (SELECT * FROM sys.schemas WHERE name = @SchemaName)
BEGIN
    DECLARE @CreateSchemaSql NVARCHAR(MAX) = N'CREATE SCHEMA [' + @SchemaName + N']'
    EXEC sp_executesql @CreateSchemaSql
    PRINT 'Schema ' + @SchemaName + ' created successfully'
END

-- =============================================================================
-- Core Tables
-- =============================================================================

-- Create the feature_flags table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'feature_flags' AND schema_id = SCHEMA_ID(@SchemaName))
BEGIN
    DECLARE @FeatureFlagsTable NVARCHAR(MAX) = N'
    CREATE TABLE [' + @SchemaName + N'].[feature_flags] (
        -- Flag uniqueness scope
        [key] NVARCHAR(255) NOT NULL,
        application_name NVARCHAR(255) NOT NULL DEFAULT ''global'',
        application_version NVARCHAR(100) NOT NULL DEFAULT ''0.0.0.0'',
        scope INT NOT NULL DEFAULT 0,
        
        -- Descriptive fields
        name NVARCHAR(500) NOT NULL,
        description NVARCHAR(MAX) NOT NULL DEFAULT '''',

        -- Evaluation modes
        evaluation_modes NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_evaluation_modes_json CHECK (ISJSON(evaluation_modes) = 1),
        
        -- Scheduling
        scheduled_enable_date DATETIMEOFFSET NULL,
        scheduled_disable_date DATETIMEOFFSET NULL,
        
        -- Time Windows
        window_start_time TIME NULL,
        window_end_time TIME NULL,
        time_zone NVARCHAR(100) NULL,
        window_days NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_window_days_json CHECK (ISJSON(window_days) = 1),
        
        -- Targeting
        targeting_rules NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_targeting_rules_json CHECK (ISJSON(targeting_rules) = 1),

        -- User-level controls
        enabled_users NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_enabled_users_json CHECK (ISJSON(enabled_users) = 1),
        disabled_users NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_disabled_users_json CHECK (ISJSON(disabled_users) = 1),
        user_percentage_enabled INT NOT NULL DEFAULT 100 
            CONSTRAINT CK_user_percentage CHECK (user_percentage_enabled >= 0 AND user_percentage_enabled <= 100),

        -- Tenant-level controls
        enabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_enabled_tenants_json CHECK (ISJSON(enabled_tenants) = 1),
        disabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_disabled_tenants_json CHECK (ISJSON(disabled_tenants) = 1),
        tenant_percentage_enabled INT NOT NULL DEFAULT 100 
            CONSTRAINT CK_tenant_percentage CHECK (tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100),
        
        -- Variations
        variations NVARCHAR(MAX) NOT NULL DEFAULT ''{}''
            CONSTRAINT CK_variations_json CHECK (ISJSON(variations) = 1),
        default_variation NVARCHAR(255) NOT NULL DEFAULT ''off'',

        -- Audit fields
        created_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
        updated_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT PK_feature_flags PRIMARY KEY ([key], application_name, application_version, scope)
    )'
    
    EXEC sp_executesql @FeatureFlagsTable
    PRINT 'Table feature_flags created successfully'
END
ELSE
BEGIN
    PRINT 'Table feature_flags already exists'
END

-- Create the metadata table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'feature_flags_metadata' AND schema_id = SCHEMA_ID(@SchemaName))
BEGIN
    DECLARE @MetadataTable NVARCHAR(MAX) = N'
    CREATE TABLE [' + @SchemaName + N'].[feature_flags_metadata] (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        flag_key NVARCHAR(255) NOT NULL,

        -- Flag uniqueness scope
        application_name NVARCHAR(255) NOT NULL DEFAULT ''global'',
        application_version NVARCHAR(100) NOT NULL DEFAULT ''0.0.0.0'',

        -- Retention and expiration
        is_permanent BIT NOT NULL DEFAULT 0,
        expiration_date DATETIMEOFFSET NOT NULL,

        -- Tags for categorization
        tags NVARCHAR(MAX) NOT NULL DEFAULT ''{}''
            CONSTRAINT CK_metadata_tags_json CHECK (ISJSON(tags) = 1),
        
        -- Audit fields
        created_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
        updated_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE()
    )'
    
    EXEC sp_executesql @MetadataTable
    PRINT 'Table feature_flags_metadata created successfully'
END
ELSE
BEGIN
    PRINT 'Table feature_flags_metadata already exists'
END

-- Create the audit table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'feature_flags_audit' AND schema_id = SCHEMA_ID(@SchemaName))
BEGIN
    DECLARE @AuditTable NVARCHAR(MAX) = N'
    CREATE TABLE [' + @SchemaName + N'].[feature_flags_audit] (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        flag_key NVARCHAR(255) NOT NULL,

        -- Flag uniqueness scope
        application_name NVARCHAR(255) NULL DEFAULT ''global'',
        application_version NVARCHAR(100) NOT NULL DEFAULT ''0.0.0.0'',

        -- Action details
        action NVARCHAR(50) NOT NULL,
        actor NVARCHAR(255) NOT NULL,
        timestamp DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
        reason NVARCHAR(MAX) NULL,
        
        -- Additional context
        ip_address NVARCHAR(45) NULL,
        user_agent NVARCHAR(MAX) NULL
    )'
    
    EXEC sp_executesql @AuditTable
    PRINT 'Table feature_flags_audit created successfully'
END
ELSE
BEGIN
    PRINT 'Table feature_flags_audit already exists'
END

-- =============================================================================
-- Schema Version Tracking
-- =============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'schema_migrations' AND schema_id = SCHEMA_ID(@SchemaName))
BEGIN
    DECLARE @MigrationsTable NVARCHAR(MAX) = N'
    CREATE TABLE [' + @SchemaName + N'].[schema_migrations] (
        version NVARCHAR(50) PRIMARY KEY,
        applied_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
        description NVARCHAR(MAX) NOT NULL
    )'
    
    EXEC sp_executesql @MigrationsTable
    PRINT 'Table schema_migrations created successfully'
END

-- Record this migration
IF NOT EXISTS (SELECT 1 FROM schema_migrations WHERE version = '1.0.0')
BEGIN
    INSERT INTO schema_migrations (version, description) 
    VALUES ('1.0.0', 'Initial feature flags schema creation')
    PRINT 'Migration 1.0.0 recorded'
END

-- =============================================================================
-- 03_create_indexes.sql - Performance Indexes
-- =============================================================================

-- Indexes for feature_flags table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_scheduled_enable')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_scheduled_enable 
        ON feature_flags (scheduled_enable_date) 
        WHERE scheduled_enable_date IS NOT NULL
    PRINT 'Index IX_feature_flags_scheduled_enable created'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_application_name')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_application_name 
        ON feature_flags (application_name)
    PRINT 'Index IX_feature_flags_application_name created'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_application_version')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_application_version 
        ON feature_flags (application_version)
    PRINT 'Index IX_feature_flags_application_version created'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_scope')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_scope 
        ON feature_flags (scope)
    PRINT 'Index IX_feature_flags_scope created'
END

-- Composite indexes for common query patterns
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_scope_app_name')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_scope_app_name 
        ON feature_flags (scope, application_name)
    PRINT 'Index IX_feature_flags_scope_app_name created'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_app_version_scope')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_app_version_scope 
        ON feature_flags (application_name, application_version, scope)
    PRINT 'Index IX_feature_flags_app_version_scope created'
END

-- Audit table indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_audit_flag_key')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_audit_flag_key 
        ON feature_flags_audit (flag_key)
    PRINT 'Index IX_feature_flags_audit_flag_key created'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_audit_timestamp')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_audit_timestamp 
        ON feature_flags_audit (timestamp)
    PRINT 'Index IX_feature_flags_audit_timestamp created'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_audit_actor')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_audit_actor 
        ON feature_flags_audit (actor)
    PRINT 'Index IX_feature_flags_audit_actor created'
END

-- Metadata table indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_metadata_flag_key')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_metadata_flag_key 
        ON feature_flags_metadata (flag_key)
    PRINT 'Index IX_feature_flags_metadata_flag_key created'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_metadata_expiration')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_metadata_expiration 
        ON feature_flags_metadata (expiration_date) 
        WHERE is_permanent = 0
    PRINT 'Index IX_feature_flags_metadata_expiration created'
END

-- =============================================================================
-- 04_create_triggers.sql - Automatic Timestamp Updates
-- =============================================================================

-- Trigger for feature_flags table
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_feature_flags_update_timestamp')
BEGIN
    DECLARE @TriggerSql NVARCHAR(MAX) = N'
    CREATE TRIGGER TR_feature_flags_update_timestamp
        ON [' + @SchemaName + N'].[feature_flags]
        AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON
        UPDATE [' + @SchemaName + N'].[feature_flags]
        SET updated_at = GETUTCDATE()
        FROM [' + @SchemaName + N'].[feature_flags] f
        INNER JOIN inserted i ON f.[key] = i.[key] 
            AND f.application_name = i.application_name 
            AND f.application_version = i.application_version 
            AND f.scope = i.scope
    END'
    
    EXEC sp_executesql @TriggerSql
    PRINT 'Trigger TR_feature_flags_update_timestamp created'
END

-- Trigger for metadata table
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_feature_flags_metadata_update_timestamp')
BEGIN
    DECLARE @MetadataTriggerSql NVARCHAR(MAX) = N'
    CREATE TRIGGER TR_feature_flags_metadata_update_timestamp
        ON [' + @SchemaName + N'].[feature_flags_metadata]
        AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON
        UPDATE [' + @SchemaName + N'].[feature_flags_metadata]
        SET updated_at = GETUTCDATE()
        FROM [' + @SchemaName + N'].[feature_flags_metadata] m
        INNER JOIN inserted i ON m.id = i.id
    END'
    
    EXEC sp_executesql @MetadataTriggerSql
    PRINT 'Trigger TR_feature_flags_metadata_update_timestamp created'
END

-- =============================================================================
-- 05_grant_permissions.sql - Security Setup (modify as needed)
-- =============================================================================

-- Create user if it doesn't exist (requires appropriate privileges)
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = @UserName)
BEGIN
    DECLARE @CreateUserSql NVARCHAR(MAX) = N'CREATE USER [' + @UserName + N'] WITHOUT LOGIN'
    EXEC sp_executesql @CreateUserSql
    PRINT 'User ' + @UserName + ' created'
END

-- Grant permissions on tables
DECLARE @GrantSql NVARCHAR(MAX) = N'
GRANT SELECT, INSERT, UPDATE ON [' + @SchemaName + N'].[feature_flags] TO [' + @UserName + N']
GRANT SELECT, INSERT, UPDATE ON [' + @SchemaName + N'].[feature_flags_metadata] TO [' + @UserName + N']
GRANT SELECT, INSERT ON [' + @SchemaName + N'].[feature_flags_audit] TO [' + @UserName + N']
GRANT SELECT ON [' + @SchemaName + N'].[schema_migrations] TO [' + @UserName + N']'

EXEC sp_executesql @GrantSql
PRINT 'Permissions granted to ' + @UserName

-- =============================================================================
-- Verification Script
-- =============================================================================

PRINT '================================================='
PRINT 'INSTALLATION VERIFICATION'
PRINT '================================================='

-- Verify tables exist
SELECT 
    t.name AS TableName,
    s.name AS SchemaName,
    t.create_date AS Created
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name IN ('feature_flags', 'feature_flags_metadata', 'feature_flags_audit', 'schema_migrations')
    AND s.name = @SchemaName
ORDER BY t.name

-- Verify indexes
SELECT 
    i.name AS IndexName,
    t.name AS TableName,
    i.type_desc AS IndexType
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name IN ('feature_flags', 'feature_flags_metadata', 'feature_flags_audit')
    AND s.name = @SchemaName
    AND i.type > 0  -- Exclude heaps
ORDER BY t.name, i.name

-- Verify migrations
SELECT * FROM schema_migrations ORDER BY applied_at

PRINT 'Installation completed successfully!'

-- =============================================================================
-- Rollback Script (rollback.sql)
-- =============================================================================

/*
-- WARNING: This will destroy all feature flag data!
-- Only use in development or with proper backups

-- Drop triggers
DROP TRIGGER IF EXISTS TR_feature_flags_update_timestamp
DROP TRIGGER IF EXISTS TR_feature_flags_metadata_update_timestamp

-- Drop indexes (they will be dropped with tables, but explicit for clarity)
DROP INDEX IF EXISTS IX_feature_flags_scheduled_enable ON feature_flags
DROP INDEX IF EXISTS IX_feature_flags_application_name ON feature_flags
-- ... (add all other indexes)

-- Drop tables in reverse dependency order
DROP TABLE IF EXISTS feature_flags_audit
DROP TABLE IF EXISTS feature_flags_metadata
DROP TABLE IF EXISTS feature_flags
DROP TABLE IF EXISTS schema_migrations

-- Drop user
DROP USER IF EXISTS [propel_user]

-- Optionally drop schema (if it was created specifically for this)
-- DROP SCHEMA IF EXISTS [your_schema]

-- Optionally drop database (be very careful!)
-- USE [master]
-- DROP DATABASE IF EXISTS [PropelFeatureFlags]
*/

-- =============================================================================
-- Usage Instructions
-- =============================================================================

/*
MANUAL SETUP:
1. Modify variables at the top of this script
2. Run the entire script as sysadmin or database owner
3. Verify installation with the verification queries
4. Test with: SELECT * FROM schema_migrations

SQLCMD SETUP:
sqlcmd -S localhost -d master -E -Q "$(type sqlserver_schema.sql)"

MIGRATION APPROACH:
- Use schema_migrations table to track versions
- Create separate files for each version (V1_0_1__add_new_column.sql)
- Always include rollback scripts
- Use transaction boundaries for complex migrations
- Test in development first

PRODUCTION CONSIDERATIONS:
- Review and adjust connection settings
- Consider backup strategy before migrations
- Test performance with realistic data volumes
- Monitor index usage and fragmentation
- Set up appropriate maintenance plans
*/