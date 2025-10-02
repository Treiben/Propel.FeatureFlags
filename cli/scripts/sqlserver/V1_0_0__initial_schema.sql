-- =============================================================================
-- Initial schema for Feature Flags system
-- Database: SQL Server
-- Scripts/Migrations/V1_0_0__initial_schema.sql
-- =============================================================================

-- Create the FeatureFlags table
CREATE TABLE [dbo].[FeatureFlags] (
    -- Flag uniqueness Scope
    [Key] NVARCHAR(255) NOT NULL,
    ApplicationName NVARCHAR(255) NOT NULL DEFAULT 'global',
    ApplicationVersion NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',
    Scope INT NOT NULL DEFAULT 0,
        
    -- Descriptive fields
    [Name] NVARCHAR(500) NOT NULL,
    [Description] NVARCHAR(MAX) NOT NULL DEFAULT '',

    -- Evaluation modes
    EvaluationModes NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_EvaluationModes_json CHECK (ISJSON(EvaluationModes) = 1),
        
    -- Scheduling
    ScheduledEnableDate DATETIMEOFFSET NULL,
    ScheduledDisableDate DATETIMEOFFSET NULL,
        
    -- Time Windows
    WindowStartTime TIME NULL,
    WindowEndTime TIME NULL,
    TimeZone NVARCHAR(100) NULL,
    WindowDays NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_WindowDays_json CHECK (ISJSON(WindowDays) = 1),
        
    -- Targeting
    TargetingRules NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_TargetingRules_json CHECK (ISJSON(TargetingRules) = 1),

    -- User-level controls
    EnabledUsers NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_EnabledUsers_json CHECK (ISJSON(EnabledUsers) = 1),
    DisabledUsers NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_DisabledUsers_json CHECK (ISJSON(DisabledUsers) = 1),
    UserPercentageEnabled INT NOT NULL DEFAULT 100 
        CONSTRAINT CK_UserPercentage CHECK (UserPercentageEnabled >= 0 AND UserPercentageEnabled <= 100),

    -- Tenant-level controls
    EnabledTenants NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_EnabledTenants_json CHECK (ISJSON(EnabledTenants) = 1),
    DisabledTenants NVARCHAR(MAX) NOT NULL DEFAULT '[]'
        CONSTRAINT CK_DisabledTenants_json CHECK (ISJSON(DisabledTenants) = 1),
    TenantPercentageEnabled INT NOT NULL DEFAULT 100 
        CONSTRAINT CK_TenantPercentage CHECK (TenantPercentageEnabled >= 0 AND TenantPercentageEnabled <= 100),
        
    -- Variations
    Variations NVARCHAR(MAX) NOT NULL DEFAULT '{}'
        CONSTRAINT CK_Variations_json CHECK (ISJSON(Variations) = 1),
    DefaultVariation NVARCHAR(255) NOT NULL DEFAULT 'off',

    CONSTRAINT PK_FeatureFlags PRIMARY KEY ([Key], ApplicationName, ApplicationVersion)
)   
PRINT 'Table FeatureFlags created successfully'


-- Create the metadata table
CREATE TABLE [dbo].[FeatureFlagsMetadata] (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FlagKey NVARCHAR(255) NOT NULL,

    -- Flag uniqueness Scope
    ApplicationName NVARCHAR(255) NOT NULL DEFAULT 'global',
    ApplicationVersion NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

    -- Retention and expiration
    IsPermanent BIT NOT NULL DEFAULT 0,
    ExpirationDate DATETIMEOFFSET NOT NULL,

    -- Tags for categorization
    Tags NVARCHAR(MAX) NOT NULL DEFAULT '{}'
        CONSTRAINT CK_metadata_tags_json CHECK (ISJSON(tags) = 1),
)  
PRINT 'Table FeatureFlagsMetadata created successfully'

-- Create the audit table
CREATE TABLE [dbo].[FeatureFlagsAudit] (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FlagKey NVARCHAR(255) NOT NULL,

    -- Flag uniqueness Scope
    ApplicationName NVARCHAR(255) NULL DEFAULT 'global',
    ApplicationVersion NVARCHAR(100) NOT NULL DEFAULT '0.0.0.0',

    -- Action details
    [Action] NVARCHAR(50) NOT NULL,
    Actor NVARCHAR(255) NOT NULL,
    [Timestamp] DATETIMEOFFSET NOT NULL DEFAULT GETUTCDATE(),
    Notes NVARCHAR(MAX) NULL,
)  
PRINT 'Table FeatureFlagsAudit created successfully'


PRINT 'Initial schema migration completed successfully'