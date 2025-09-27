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

-- Create the FeatureFlags table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FeatureFlags' AND schema_id = SCHEMA_ID(@SchemaName))
BEGIN
    DECLARE @FeatureFlagsTable NVARCHAR(MAX) = N'
    CREATE TABLE [' + @SchemaName + N'].[FeatureFlags] (
        -- Flag uniqueness scope
        [key] NVARCHAR(255) NOT NULL,
        ApplicationName NVARCHAR(255) NOT NULL DEFAULT ''global'',
        ApplicationVersion NVARCHAR(100) NOT NULL DEFAULT ''0.0.0.0'',
        Scope INT NOT NULL DEFAULT 0,
        
        -- Descriptive fields
        Name NVARCHAR(500) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL DEFAULT '''',

        -- Evaluation modes
        EvaluationModes NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_EvaluationModes_json CHECK (ISJSON(EvaluationModes) = 1),
        
        -- Scheduling
        ScheduledEnableDate DATETIMEOFFSET NULL,
        ScheduledDisableDate DATETIMEOFFSET NULL,
        
        -- Time Windows
        WindowStartTime TIME NULL,
        WindowEndTime TIME NULL,
        TimeZone NVARCHAR(100) NULL,
        WindowDays NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_WindowDays_json CHECK (ISJSON(WindowDays) = 1),
        
        -- Targeting
        TargetingRules NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_TargetingRules_json CHECK (ISJSON(TargetingRules) = 1),

        -- User-level controls
        EnabledUsers NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_EnabledUsers_json CHECK (ISJSON(EnabledUsers) = 1),
        DisabledUsers NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_DisabledUsers_json CHECK (ISJSON(DisabledUsers) = 1),
        UserPercentageEnabled INT NOT NULL DEFAULT 100 
            CONSTRAINT CK_UserPercentage CHECK (UserPercentageEnabled >= 0 AND UserPercentageEnabled <= 100),

        -- Tenant-level controls
        EnabledTenants NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_EnabledTenants_json CHECK (ISJSON(EnabledTenants) = 1),
        DisabledTenants NVARCHAR(MAX) NOT NULL DEFAULT ''[]''
            CONSTRAINT CK_DisabledTenants_json CHECK (ISJSON(DisabledTenants) = 1),
        TenantPercentageEnabled INT NOT NULL DEFAULT 100 
            CONSTRAINT CK_TenantPercentage CHECK (TenantPercentageEnabled >= 0 AND TenantPercentageEnabled <= 100),
        
        -- Variations
        Variations NVARCHAR(MAX) NOT NULL DEFAULT ''{}''
            CONSTRAINT CK_Variations_json CHECK (ISJSON(Variations) = 1),
        default_variation NVARCHAR(255) NOT NULL DEFAULT ''off'',

        CONSTRAINT PK_FeatureFlags PRIMARY KEY ([key], ApplicationName, ApplicationVersion)
    )'
    
    EXEC sp_executesql @FeatureFlagsTable
    PRINT 'Table FeatureFlags created successfully'
END
ELSE
BEGIN
    PRINT 'Table FeatureFlags already exists'
END

-- Create the metadata table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FeatureFlagsMetadata' AND schema_id = SCHEMA_ID(@SchemaName))
BEGIN
    DECLARE @MetadataTable NVARCHAR(MAX) = N'
    CREATE TABLE [' + @SchemaName + N'].[FeatureFlagsMetadata] (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        FlagKey NVARCHAR(255) NOT NULL,

        -- Flag uniqueness scope
        ApplicationName NVARCHAR(255) NOT NULL DEFAULT ''global'',
        ApplicationVersion NVARCHAR(100) NOT NULL DEFAULT ''0.0.0.0'',

        -- Retention and expiration
        IsPermanent BIT NOT NULL DEFAULT 0,
        ExpirationDate DATETIMEOFFSET NOT NULL,

        -- Tags for categorization
        Tags NVARCHAR(MAX) NOT NULL DEFAULT ''{}''
            CONSTRAINT CK_metadata_tags_json CHECK (ISJSON(tags) = 1)
    )'
    
    EXEC sp_executesql @MetadataTable
    PRINT 'Table FeatureFlagsMetadata created successfully'
END
ELSE
BEGIN
    PRINT 'Table FeatureFlagsMetadata already exists'
END

-- Create the audit table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FeatureFlagsAudit' AND schema_id = SCHEMA_ID(@SchemaName))
BEGIN
    DECLARE @AuditTable NVARCHAR(MAX) = N'
    CREATE TABLE [' + @SchemaName + N'].[FeatureFlagsAudit] (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        FlagKey NVARCHAR(255) NOT NULL,

        -- Flag uniqueness scope
        ApplicationName NVARCHAR(255) NULL DEFAULT ''global'',
        ApplicationVersion NVARCHAR(100) NOT NULL DEFAULT ''0.0.0.0'',

        -- Action details
        Action NVARCHAR(50) NOT NULL,
        Actor NVARCHAR(255) NOT NULL,
        Timestamp DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
        Notes NVARCHAR(MAX) NULL
    )'
    
    EXEC sp_executesql @AuditTable
    PRINT 'Table FeatureFlagsAudit created successfully'
END
ELSE
BEGIN
    PRINT 'Table FeatureFlagsAudit already exists'
END

-- =============================================================================
-- Schema Version Tracking
-- =============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PropelSchemaMigrations' AND schema_id = SCHEMA_ID(@SchemaName))
BEGIN
    DECLARE @MigrationsTable NVARCHAR(MAX) = N'
    CREATE TABLE [' + @SchemaName + N'].[PropelSchemaMigrations] (
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
GRANT SELECT, INSERT, UPDATE ON [' + @SchemaName + N'].[FeatureFlags] TO [' + @UserName + N']
GRANT SELECT, INSERT, UPDATE ON [' + @SchemaName + N'].[FeatureFlagsMetadata] TO [' + @UserName + N']
GRANT SELECT, INSERT ON [' + @SchemaName + N'].[FeatureFlagsAudit] TO [' + @UserName + N']
GRANT SELECT ON [' + @SchemaName + N'].[PropelSchemaMigrations] TO [' + @UserName + N']'

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
WHERE t.name IN ('FeatureFlags', 'FeatureFlagsMetadata', 'FeatureFlagsAudit', 'PropelSchemaMigrations')
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
WHERE t.name IN ('FeatureFlags', 'FeatureFlagsMetadata', 'FeatureFlagsAudit')
    AND s.name = @SchemaName
    AND i.type > 0  -- Exclude heaps
ORDER BY t.name, i.name

-- Verify migrations
SELECT * FROM schema_migrations ORDER BY applied_at

PRINT 'Installation completed successfully!'

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