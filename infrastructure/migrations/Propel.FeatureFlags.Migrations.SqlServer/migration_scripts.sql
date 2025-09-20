-- =============================================================================
-- Scripts/Migrations/V1_0_0__initial_schema.sql
-- =============================================================================

-- Create the feature_flags table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'feature_flags' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[feature_flags] (
        -- Flag uniqueness scope
        [key] NVARCHAR(255) NOT NULL,
        application_name NVARCHAR(255) NOT NULL DEFAULT 'global',
        application_version NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',
        scope INT NOT NULL DEFAULT 0,
        
        -- Descriptive fields
        name NVARCHAR(500) NOT NULL,
        description NVARCHAR(MAX) NOT NULL DEFAULT '',

        -- Evaluation modes
        evaluation_modes NVARCHAR(MAX) NOT NULL DEFAULT '[]'
            CONSTRAINT CK_evaluation_modes_json CHECK (ISJSON(evaluation_modes) = 1),
        
        -- Scheduling
        scheduled_enable_date DATETIMEOFFSET NULL,
        scheduled_disable_date DATETIMEOFFSET NULL,
        
        -- Time Windows
        window_start_time TIME NULL,
        window_end_time TIME NULL,
        time_zone NVARCHAR(100) NULL,
        window_days NVARCHAR(MAX) NOT NULL DEFAULT '[]'
            CONSTRAINT CK_window_days_json CHECK (ISJSON(window_days) = 1),
        
        -- Targeting
        targeting_rules NVARCHAR(MAX) NOT NULL DEFAULT '[]'
            CONSTRAINT CK_targeting_rules_json CHECK (ISJSON(targeting_rules) = 1),

        -- User-level controls
        enabled_users NVARCHAR(MAX) NOT NULL DEFAULT '[]'
            CONSTRAINT CK_enabled_users_json CHECK (ISJSON(enabled_users) = 1),
        disabled_users NVARCHAR(MAX) NOT NULL DEFAULT '[]'
            CONSTRAINT CK_disabled_users_json CHECK (ISJSON(disabled_users) = 1),
        user_percentage_enabled INT NOT NULL DEFAULT 100 
            CONSTRAINT CK_user_percentage CHECK (user_percentage_enabled >= 0 AND user_percentage_enabled <= 100),

        -- Tenant-level controls
        enabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT '[]'
            CONSTRAINT CK_enabled_tenants_json CHECK (ISJSON(enabled_tenants) = 1),
        disabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT '[]'
            CONSTRAINT CK_disabled_tenants_json CHECK (ISJSON(disabled_tenants) = 1),
        tenant_percentage_enabled INT NOT NULL DEFAULT 100 
            CONSTRAINT CK_tenant_percentage CHECK (tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100),
        
        -- Variations
        variations NVARCHAR(MAX) NOT NULL DEFAULT '{}'
            CONSTRAINT CK_variations_json CHECK (ISJSON(variations) = 1),
        default_variation NVARCHAR(255) NOT NULL DEFAULT 'off',

        -- Audit fields
        created_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
        updated_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT PK_feature_flags PRIMARY KEY ([key], application_name, application_version, scope)
    )
    
    PRINT 'Table feature_flags created successfully'
END
GO

-- Create the metadata table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'feature_flags_metadata' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[feature_flags_metadata] (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        flag_key NVARCHAR(255) NOT NULL,

        -- Flag uniqueness scope
        application_name NVARCHAR(255) NOT NULL DEFAULT 'global',
        application_version NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

        -- Retention and expiration
        is_permanent BIT NOT NULL DEFAULT 0,
        expiration_date DATETIMEOFFSET NOT NULL,

        -- Tags for categorization
        tags NVARCHAR(MAX) NOT NULL DEFAULT '{}'
            CONSTRAINT CK_metadata_tags_json CHECK (ISJSON(tags) = 1),
        
        -- Audit fields
        created_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
        updated_at DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE()
    )
    
    PRINT 'Table feature_flags_metadata created successfully'
END
GO

-- Create the audit table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'feature_flags_audit' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[feature_flags_audit] (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        flag_key NVARCHAR(255) NOT NULL,

        -- Flag uniqueness scope
        application_name NVARCHAR(255) NULL DEFAULT 'global',
        application_version NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

        -- Action details
        action NVARCHAR(50) NOT NULL,
        actor NVARCHAR(255) NOT NULL,
        timestamp DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
        reason NVARCHAR(MAX) NULL,
        
        -- Additional context
        ip_address NVARCHAR(45) NULL,
        user_agent NVARCHAR(MAX) NULL
    )
    
    PRINT 'Table feature_flags_audit created successfully'
END
GO

PRINT 'Initial schema migration completed successfully'

-- =============================================================================
-- Scripts/Migrations/V1_0_1__create_indexes.sql
-- =============================================================================

-- Indexes for feature_flags table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_scheduled_enable')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_scheduled_enable 
        ON feature_flags (scheduled_enable_date) 
        WHERE scheduled_enable_date IS NOT NULL
    PRINT 'Index IX_feature_flags_scheduled_enable created'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_application_name')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_application_name 
        ON feature_flags (application_name)
    PRINT 'Index IX_feature_flags_application_name created'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_scope_app_name')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_scope_app_name 
        ON feature_flags (scope, application_name)
    PRINT 'Index IX_feature_flags_scope_app_name created'
END
GO

-- Audit table indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_audit_flag_key')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_audit_flag_key 
        ON feature_flags_audit (flag_key)
    PRINT 'Index IX_feature_flags_audit_flag_key created'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_feature_flags_audit_timestamp')
BEGIN
    CREATE NONCLUSTERED INDEX IX_feature_flags_audit_timestamp 
        ON feature_flags_audit (timestamp)
    PRINT 'Index IX_feature_flags_audit_timestamp created'
END
GO

PRINT 'Index creation migration completed successfully'

-- =============================================================================
-- Scripts/Migrations/V1_0_2__create_triggers.sql
-- =============================================================================

-- Trigger for feature_flags table
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_feature_flags_update_timestamp')
BEGIN
    EXEC('
    CREATE TRIGGER TR_feature_flags_update_timestamp
        ON [dbo].[feature_flags]
        AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON
        UPDATE [dbo].[feature_flags]
        SET updated_at = GETUTCDATE()
        FROM [dbo].[feature_flags] f
        INNER JOIN inserted i ON f.[key] = i.[key] 
            AND f.application_name = i.application_name 
            AND f.application_version = i.application_version 
            AND f.scope = i.scope
    END')
    
    PRINT 'Trigger TR_feature_flags_update_timestamp created'
END
GO

-- Trigger for metadata table
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_feature_flags_metadata_update_timestamp')
BEGIN
    EXEC('
    CREATE TRIGGER TR_feature_flags_metadata_update_timestamp
        ON [dbo].[feature_flags_metadata]
        AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON
        UPDATE [dbo].[feature_flags_metadata]
        SET updated_at = GETUTCDATE()
        FROM [dbo].[feature_flags_metadata] m
        INNER JOIN inserted i ON m.id = i.id
    END')
    
    PRINT 'Trigger TR_feature_flags_metadata_update_timestamp created'
END
GO

PRINT 'Trigger creation migration completed successfully'

-- =============================================================================
-- Scripts/Migrations/Rollback/V1_0_2__create_triggers.sql
-- =============================================================================

-- Drop triggers
DROP TRIGGER IF EXISTS TR_feature_flags_update_timestamp
DROP TRIGGER IF EXISTS TR_feature_flags_metadata_update_timestamp

PRINT 'Triggers dropped successfully'

-- =============================================================================
-- Scripts/Migrations/Rollback/V1_0_1__create_indexes.sql
-- =============================================================================

-- Drop indexes
DROP INDEX IF EXISTS IX_feature_flags_scheduled_enable ON feature_flags
DROP INDEX IF EXISTS IX_feature_flags_application_name ON feature_flags
DROP INDEX IF EXISTS IX_feature_flags_scope_app_name ON feature_flags
DROP INDEX IF EXISTS IX_feature_flags_audit_flag_key ON feature_flags_audit
DROP INDEX IF EXISTS IX_feature_flags_audit_timestamp ON feature_flags_audit

PRINT 'Indexes dropped successfully'

-- =============================================================================
-- Scripts/Migrations/Rollback/V1_0_0__initial_schema.sql
-- =============================================================================

-- WARNING: This will destroy all feature flag data!
-- Only use in development or with proper backups

-- Drop tables in reverse dependency order
DROP TABLE IF EXISTS feature_flags_audit
DROP TABLE IF EXISTS feature_flags_metadata
DROP TABLE IF EXISTS feature_flags

PRINT 'Schema rollback completed - all tables dropped'