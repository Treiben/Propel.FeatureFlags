-- ====================================================================
-- PROPEL FEATURE FLAGS - POSTGRESQL DATABASE INITIALIZATION SCRIPT
-- ====================================================================
-- This script demonstrates comprehensive feature flag configurations
-- using the new evaluation_modes JSONB array structure.
-- Updated to match PostgreSQLDatabaseInitializer schema.

-- Create the feature flags schema if it doesn't exist
CREATE SCHEMA IF NOT EXISTS usage_demo;

-- Create UUID extension for the audit table
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create the feature_flags table matching PostgreSQLDatabaseInitializer schema
CREATE TABLE IF NOT EXISTS usage_demo.feature_flags (
    key VARCHAR(255) PRIMARY KEY,
    name VARCHAR(500) NOT NULL,
    description TEXT NOT NULL DEFAULT '',

    -- Evaluation modes
    evaluation_modes JSONB NOT NULL DEFAULT '[]',

    -- Audit fields
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NULL,
    created_by VARCHAR(255) NOT NULL,
    updated_by VARCHAR(255) NULL,
    
    -- Lifecycle
    is_permanent BOOLEAN NOT NULL DEFAULT FALSE,
    expiration_date TIMESTAMP WITH TIME ZONE NOT NULL,
    
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
    user_percentage_enabled INTEGER NOT NULL DEFAULT 0 CHECK (user_percentage_enabled >= 0 AND user_percentage_enabled <= 100),

    -- Tenant-level controls
    enabled_tenants JSONB NOT NULL DEFAULT '[]',
    disabled_tenants JSONB NOT NULL DEFAULT '[]',
    tenant_percentage_enabled INTEGER NOT NULL DEFAULT 0 CHECK (tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100),
    
    -- Variations
    variations JSONB NOT NULL DEFAULT '{}',
    default_variation VARCHAR(255) NOT NULL DEFAULT 'off',
    
    -- Tags for categorization
    tags JSONB NOT NULL DEFAULT '{}'
);

-- Create the feature_flag_audit table
CREATE TABLE IF NOT EXISTS usage_demo.feature_flag_audit (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    flag_key VARCHAR(255) NOT NULL,
    action VARCHAR(50) NOT NULL,
    changed_by VARCHAR(255) NOT NULL,
    changed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    old_values JSONB NULL,
    new_values JSONB NULL,
    reason TEXT NULL
);

-- Create indexes for feature_flags table
CREATE INDEX IF NOT EXISTS ix_feature_flags_evaluation_modes ON usage_demo.feature_flags USING GIN(evaluation_modes);
CREATE INDEX IF NOT EXISTS idx_feature_flags_created_at ON usage_demo.feature_flags (created_at);
CREATE INDEX IF NOT EXISTS idx_feature_flags_updated_at ON usage_demo.feature_flags (updated_at);
CREATE INDEX IF NOT EXISTS idx_feature_flags_expiration_date ON usage_demo.feature_flags (expiration_date) WHERE expiration_date IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_feature_flags_scheduled_enable ON usage_demo.feature_flags (scheduled_enable_date) WHERE scheduled_enable_date IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_feature_flags_tags ON usage_demo.feature_flags USING GIN (tags);
CREATE INDEX IF NOT EXISTS idx_feature_flags_enabled_users ON usage_demo.feature_flags USING GIN (enabled_users);
CREATE INDEX IF NOT EXISTS idx_feature_flags_enabled_tenants ON usage_demo.feature_flags USING GIN (enabled_tenants);
CREATE INDEX IF NOT EXISTS idx_feature_flags_disabled_tenants ON usage_demo.feature_flags USING GIN (disabled_tenants);

-- Create indexes for feature_flag_audit table
CREATE INDEX IF NOT EXISTS idx_feature_flag_audit_flag_key ON usage_demo.feature_flag_audit (flag_key);
CREATE INDEX IF NOT EXISTS idx_feature_flag_audit_changed_at ON usage_demo.feature_flag_audit (changed_at);
CREATE INDEX IF NOT EXISTS idx_feature_flag_audit_changed_by ON usage_demo.feature_flag_audit (changed_by);

-- ====================================================================
-- COMPREHENSIVE FEATURE FLAG EVALUATION MODE EXAMPLES
-- ====================================================================
-- These examples demonstrate all valid evaluation_modes combinations
-- Disabled = 0, Enabled = 1, Scheduled = 2, TimeWindow = 3, 
-- UserTargeted = 4, UserRolloutPercentage = 5, TenantRolloutPercentage = 6

-- ====================================================================
-- 1. DISABLED FLAGS (evaluation_modes: [0])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, expiration_date
) VALUES (
    'new-email-service',
    'New Email Service',
    'Use the new email service implementation instead of legacy - currently disabled',
    '[0]', -- Disabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "notifications", "type": "implementation", "status": "disabled"}',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, is_permanent, expiration_date
) VALUES (
    'api-maintenance',
    'API Maintenance Mode',
    'When enabled, API endpoints return maintenance responses - disabled by default',
    '[0]', -- Disabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "api", "type": "maintenance", "status": "disabled"}',
    true,
    NOW() + INTERVAL '10 years'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- 2. ENABLED FLAGS (evaluation_modes: [1])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, expiration_date
) VALUES (
    'new-product-api',
    'New Product API',
    'Enable the new product API with enhanced product listings - fully enabled',
    '[1]', -- Enabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "products", "type": "implementation", "component": "api", "status": "enabled"}',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- 3. SCHEDULED FLAGS (evaluation_modes: [2])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, scheduled_enable_date, scheduled_disable_date, expiration_date
) VALUES (
    'featured-products-launch',
    'Featured Products Launch',
    'Scheduled launch of new featured products with special promotion',
    '[2]', -- Scheduled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "products", "type": "scheduled-launch", "component": "featured", "campaign": "q4-launch", "status": "scheduled"}',
    NOW() + INTERVAL '1 hour', -- Enable in 1 hour
    NOW() + INTERVAL '30 days', -- Disable after 30 days
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- 4. TIME WINDOW FLAGS (evaluation_modes: [3])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, window_start_time, window_end_time, time_zone, window_days, expiration_date
) VALUES (
    'flash-sale-window',
    'Flash Sale Time Window',
    'Shows flash sale products only during business hours (9 AM - 6 PM EST, weekdays)',
    '[3]', -- TimeWindow
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "products", "type": "time-window", "component": "flash-sale", "promotion": "business-hours", "status": "time-window"}',
    '09:00:00', -- 9 AM
    '18:00:00', -- 6 PM
    'America/New_York', -- EST timezone
    '[1, 2, 3, 4, 5]', -- Monday through Friday (1=Monday, 7=Sunday)
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- 5. USER TARGETED FLAGS (evaluation_modes: [4])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'recommendation-algorithm',
    'Recommendation Algorithm Selection',
    'Choose which recommendation algorithm to use based on user attributes',
    '[4]', -- UserTargeted
    'system',
    'system',
    '{"collaborative-filtering": "collaborative-filtering", "content-based": "content-based", "machine-learning": "machine-learning"}',
    'collaborative-filtering',
    '{"service": "recommendations", "type": "algorithm", "component": "engine", "status": "user-targeted"}',
    '[
        {
            "attribute": "userType",
            "operator": 0,
            "values": ["premium", "enterprise"],
            "variation": "machine-learning"
        },
        {
            "attribute": "country",
            "operator": 0,
            "values": ["US", "CA", "UK"],
            "variation": "content-based"
        }
    ]',
    '["user123", "alice.johnson", "premium-user-456", "ml-tester-789", "data-scientist-001"]',
    '["blocked-user-999", "test-account-disabled", "spam-user-123", "violator-456"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO UPDATE SET 
    targeting_rules = EXCLUDED.targeting_rules,
    enabled_users = EXCLUDED.enabled_users,
    disabled_users = EXCLUDED.disabled_users;

-- ====================================================================
-- 6. USER ROLLOUT PERCENTAGE FLAGS (evaluation_modes: [5])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, user_percentage_enabled, expiration_date
) VALUES (
    'new-dashboard-ui',
    'New Dashboard UI Rollout',
    'Progressive rollout of the new dashboard interface to users',
    '[5]', -- UserRolloutPercentage
    'system',
    'system',
    '{"new": "new-dashboard", "legacy": "old-dashboard"}',
    'legacy',
    '{"service": "dashboard", "type": "ui-rollout", "status": "user-percentage"}',
    25, -- 25% gradual rollout
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- 7. TENANT ROLLOUT PERCENTAGE FLAGS (evaluation_modes: [6])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, tenant_percentage_enabled, expiration_date
) VALUES (
    'tenant-percentage-rollout',
    'New Dashboard Tenant Rollout',
    'Progressive rollout of new dashboard to 60% of tenants for gradual deployment',
    '[6]', -- TenantRolloutPercentage
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "rollout", "type": "percentage", "component": "dashboard", "status": "tenant-percentage"}',
    60, -- 60% of tenants
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- 8. SCHEDULED WITH TIME WINDOW (evaluation_modes: [2, 3])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, 
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days, expiration_date
) VALUES (
    'holiday-promotions',
    'Holiday Promotions with Business Hours',
    'Holiday promotions active only during scheduled period and within business hours',
    '[2, 3]', -- Scheduled + TimeWindow
    'system',
    'system',
    '{"holiday": "holiday-pricing", "regular": "standard-pricing", "off": "disabled"}',
    'off',
    '{"event": "holidays", "type": "promotional", "constraint": "business-hours", "status": "scheduled-with-time-window"}',
    NOW() + INTERVAL '2 days',  -- Start holiday promotion in 2 days
    NOW() + INTERVAL '10 days', -- End holiday promotion in 10 days
    '08:00:00', -- 8 AM
    '20:00:00', -- 8 PM
    'America/New_York',
    '[1, 2, 3, 4, 5, 6]', -- Monday through Saturday
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- 9. SCHEDULED WITH USER TARGETING (evaluation_modes: [2, 4])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags,
    scheduled_enable_date, scheduled_disable_date, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'beta-features-preview',
    'Scheduled Beta Features Preview',
    'Beta features available to specific user groups during preview period',
    '[2, 4]', -- Scheduled + UserTargeted
    'system',
    'system',
    '{"beta": "beta-features", "standard": "regular-features"}',
    'standard',
    '{"type": "beta-preview", "audience": "targeted", "status": "scheduled-with-user-targeting"}',
    NOW() + INTERVAL '3 days',  -- Start beta preview in 3 days
    NOW() + INTERVAL '21 days', -- End preview in 3 weeks
    '[
        {
            "attribute": "betaTester",
            "operator": 0,
            "values": ["true"],
            "variation": "beta"
        },
        {
            "attribute": "userLevel",
            "operator": 0,
            "values": ["power-user", "enterprise"],
            "variation": "beta"
        }
    ]',
    '["beta-tester-001", "power-user-alice", "early-adopter-bob", "qa-engineer-charlie", "product-manager-diana"]',
    '["conservative-user-001", "stability-focused-eve", "production-only-frank", "risk-averse-grace"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO UPDATE SET 
    targeting_rules = EXCLUDED.targeting_rules,
    enabled_users = EXCLUDED.enabled_users,
    disabled_users = EXCLUDED.disabled_users;

-- ====================================================================
-- 10. SCHEDULED WITH USER ROLLOUT PERCENTAGE (evaluation_modes: [2, 5])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, 
    scheduled_enable_date, scheduled_disable_date, user_percentage_enabled, expiration_date
) VALUES (
    'premium-features-rollout',
    'Scheduled Premium Features Rollout',
    'Gradual rollout of premium features starting at scheduled time',
    '[2, 5]', -- Scheduled + UserRolloutPercentage
    'system',
    'system',
    '{"premium": "premium-tier", "standard": "standard-tier"}',
    'standard',
    '{"service": "premium", "type": "scheduled-rollout", "status": "scheduled-with-user-percentage"}',
    NOW() + INTERVAL '1 day',  -- Start rollout tomorrow
    NOW() + INTERVAL '60 days', -- Complete rollout in 60 days
    40, -- Start with 40% of users
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- 11. TIME WINDOW WITH USER TARGETING (evaluation_modes: [3, 4])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags,
    window_start_time, window_end_time, time_zone, window_days, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'priority-support-hours',
    'Priority Support During Business Hours',
    'Enhanced support features for premium users during business hours',
    '[3, 4]', -- TimeWindow + UserTargeted
    'system',
    'system',
    '{"priority": "priority-support", "standard": "regular-support"}',
    'standard',
    '{"service": "support", "type": "priority", "constraint": "business-hours", "status": "time-window-with-user-targeting"}',
    '08:00:00', -- 8 AM
    '18:00:00', -- 6 PM
    'America/New_York',
    '[1, 2, 3, 4, 5]', -- Weekdays
    '[
        {
            "attribute": "subscriptionTier",
            "operator": 0,
            "values": ["premium", "enterprise"],
            "variation": "priority"
        },
        {
            "attribute": "supportLevel",
            "operator": 0,
            "values": ["gold", "platinum"],
            "variation": "priority"
        }
    ]',
    '["premium-customer-001", "enterprise-admin-alice", "gold-support-bob", "platinum-user-charlie", "vip-customer-diana"]',
    '["basic-user-001", "free-tier-eve", "trial-account-frank", "suspended-user-grace"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO UPDATE SET 
    targeting_rules = EXCLUDED.targeting_rules,
    enabled_users = EXCLUDED.enabled_users,
    disabled_users = EXCLUDED.disabled_users;

-- ====================================================================
-- 12. TIME WINDOW WITH USER ROLLOUT PERCENTAGE (evaluation_modes: [3, 5])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags,
    window_start_time, window_end_time, time_zone, window_days, 
    user_percentage_enabled, expiration_date
) VALUES (
    'peak-hours-optimization',
    'Peak Hours Performance Optimization',
    'Enable performance optimizations for subset of users during peak hours',
    '[3, 5]', -- TimeWindow + UserRolloutPercentage
    'system',
    'system',
    '{"optimized": "performance-mode", "standard": "normal-mode"}',
    'standard',
    '{"service": "performance", "type": "optimization", "constraint": "peak-hours", "status": "time-window-with-user-percentage"}',
    '10:00:00', -- 10 AM
    '14:00:00', -- 2 PM
    'America/New_York',
    '[1, 2, 3, 4, 5]', -- Weekdays only
    60, -- 60% of users during peak hours
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- 13. USER TARGETING WITH USER ROLLOUT PERCENTAGE (evaluation_modes: [4, 5])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, targeting_rules, user_percentage_enabled, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'new-payment-processor',
    'New Payment Processor Integration',
    'Progressive rollout of enhanced payment processing system with user targeting',
    '[4, 5]', -- UserTargeted + UserRolloutPercentage
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "payments", "type": "processor-migration", "component": "service-layer", "criticality": "high", "status": "user-targeted-with-percentage"}',
    '[
        {
            "attribute": "riskScore",
            "operator": 7,
            "values": ["0.3"],
            "variation": "on"
        },
        {
            "attribute": "country",
            "operator": 0,
            "values": ["US", "CA"],
            "variation": "on"
        }
    ]',
    25, -- Conservative 25% rollout for critical payment infrastructure
    '["trusted-merchant-001", "verified-user-alice", "low-risk-bob", "payment-tester-charlie", "finance-team-diana"]',
    '["high-risk-user-001", "suspicious-account-eve", "fraud-flagged-frank", "blocked-merchant-grace"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO UPDATE SET 
    targeting_rules = EXCLUDED.targeting_rules,
    enabled_users = EXCLUDED.enabled_users,
    disabled_users = EXCLUDED.disabled_users;

-- ====================================================================
-- 14. COMPLEX COMBINATION: SCHEDULED + TIME WINDOW + USER TARGETING (evaluation_modes: [2, 3, 4])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags,
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'vip-event-access',
    'VIP Event Access with Limited Hours',
    'VIP users get access to special events during scheduled period and specific hours',
    '[2, 3, 4]', -- Scheduled + TimeWindow + UserTargeted
    'system',
    'system',
    '{"vip": "vip-access", "standard": "regular-access"}',
    'standard',
    '{"event": "vip-exclusive", "type": "access-control", "constraint": "scheduled-hours", "status": "scheduled-time-window-user-targeted"}',
    NOW() + INTERVAL '5 days',  -- Event starts in 5 days
    NOW() + INTERVAL '12 days', -- Event runs for a week
    '19:00:00', -- 7 PM
    '23:00:00', -- 11 PM
    'America/New_York',
    '[6, 7]', -- Weekends only for VIP events
    '[
        {
            "attribute": "vipStatus",
            "operator": 0,
            "values": ["gold", "platinum", "diamond"],
            "variation": "vip"
        },
        {
            "attribute": "eventInvited",
            "operator": 0,
            "values": ["true"],
            "variation": "vip"
        }
    ]',
    '["vip-member-001", "gold-tier-alice", "platinum-user-bob", "diamond-member-charlie", "event-organizer-diana", "special-guest-eve"]',
    '["banned-user-001", "policy-violator-frank", "restricted-account-grace", "underage-user-henry"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO UPDATE SET 
    targeting_rules = EXCLUDED.targeting_rules,
    enabled_users = EXCLUDED.enabled_users,
    disabled_users = EXCLUDED.disabled_users;

-- ====================================================================
-- 15. MAXIMUM COMPLEXITY: SCHEDULED + TIME WINDOW + USER TARGETING + USER PERCENTAGE (evaluation_modes: [2, 3, 4, 5])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags,
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days,
    user_percentage_enabled, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'ultimate-premium-experience',
    'Ultimate Premium Experience - Complete Feature Showcase',
    'The most sophisticated feature flag combining scheduling, time windows, user targeting, and percentage rollout',
    '[2, 3, 4, 5]', -- Scheduled + TimeWindow + UserTargeted + UserRolloutPercentage
    'system',
    'system',
    '{"ultimate": "premium-experience", "enhanced": "improved-features", "standard": "regular-features"}',
    'standard',
    '{"tier": "ultimate", "complexity": "maximum", "showcase": "complete", "status": "all-modes-combined"}',
    NOW() + INTERVAL '7 days',   -- Start premium experience in a week
    NOW() + INTERVAL '60 days',  -- Run for 2 months
    '10:00:00', -- 10 AM
    '16:00:00', -- 4 PM
    'America/New_York',
    '[1, 2, 3, 4, 5]', -- Weekdays only for premium experience
    20, -- Only 20% of eligible users get this ultimate experience
    '[
        {
            "attribute": "subscriptionTier",
            "operator": 0,
            "values": ["enterprise", "platinum"],
            "variation": "ultimate"
        },
        {
            "attribute": "accountValue",
            "operator": 7,
            "values": ["10000"],
            "variation": "ultimate"
        },
        {
            "attribute": "betaOptIn",
            "operator": 0,
            "values": ["true"],
            "variation": "enhanced"
        }
    ]',
    '["enterprise-admin-001", "platinum-member-alice", "high-value-bob", "beta-champion-charlie", "product-evangelist-diana"]',
    '["budget-user-001", "basic-tier-eve", "trial-expired-frank", "inactive-account-grace"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO UPDATE SET 
    targeting_rules = EXCLUDED.targeting_rules,
    enabled_users = EXCLUDED.enabled_users,
    disabled_users = EXCLUDED.disabled_users;

-- ====================================================================
-- 16. TENANT-SPECIFIC FEATURE FLAGS
-- ====================================================================

-- Enable tenant-specific features
INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, expiration_date
) VALUES (
    'tenant-premium-features',
    'Premium Features for Tenants',
    'Enable advanced features for premium and enterprise tenants only',
    '[1]', -- Enabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "premium", "type": "access-control", "tier": "premium", "status": "enabled-for-tenants"}',
    '["premium-corp", "enterprise-solutions", "vip-client-alpha", "mega-corp-beta"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- Tenant rollout with percentage and tenant targeting combined
INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, disabled_tenants, tenant_percentage_enabled, expiration_date
) VALUES (
    'tenant-beta-program',
    'Multi-Tenant Beta Program',
    'Beta features with tenant percentage rollout plus explicit inclusions/exclusions',
    '[6]', -- TenantRolloutPercentage
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "multi", "type": "beta-program", "phase": "phase1", "status": "tenant-percentage-with-lists"}',
    '["beta-tester-1", "beta-tester-2", "early-adopter-corp", "tech-forward-inc"]',
    '["conservative-corp", "legacy-systems-ltd", "security-first-org", "compliance-strict-co"]',
    40, -- 40% of remaining tenants (after explicit inclusions/exclusions)
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- 17. ROLE AND DEPARTMENT BASED TARGETING (evaluation_modes: [4])
-- ====================================================================
INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'admin-panel-enabled',
    'Admin Panel Access Control',
    'Controls access to sensitive admin operations based on user role and department',
    '[4]', -- UserTargeted
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "admin", "type": "access-control", "component": "panel", "security": "high", "status": "user-targeted"}',
    '[
        {
            "attribute": "role",
            "operator": 0,
            "values": ["admin", "super-admin"],
            "variation": "on"
        },
        {
            "attribute": "department",
            "operator": 0,
            "values": ["engineering", "operations"],
            "variation": "on"
        }
    ]',
    '["admin-alice", "super-admin-bob", "ops-manager-charlie", "security-lead-diana", "system-admin-eve"]',
    '["intern-frank", "guest-user-grace", "external-contractor-henry", "readonly-user-ivy"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO UPDATE SET 
    targeting_rules = EXCLUDED.targeting_rules,
    evaluation_modes = EXCLUDED.evaluation_modes,
    description = EXCLUDED.description,
    enabled_users = EXCLUDED.enabled_users,
    disabled_users = EXCLUDED.disabled_users;

-- Function to automatically update the updated_at timestamp
CREATE OR REPLACE FUNCTION usage_demo.update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Trigger to automatically update updated_at
DROP TRIGGER IF EXISTS update_feature_flags_updated_at ON usage_demo.feature_flags;
CREATE TRIGGER update_feature_flags_updated_at 
    BEFORE UPDATE ON usage_demo.feature_flags 
    FOR EACH ROW 
    EXECUTE FUNCTION usage_demo.update_updated_at_column();

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, expiration_date
) VALUES (
    'legacy-checkout-v1',
    'Legacy Checkout System V1',
    'Old checkout system that was deprecated and expired yesterday',
    '[0]', -- Disabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "checkout", "type": "legacy", "status": "expired", "deprecated": "true"}',
    NOW() - INTERVAL '1 day' -- Expired yesterday
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, user_percentage_enabled, expiration_date
) VALUES (
    'old-search-algorithm',
    'Deprecated Search Algorithm',
    'Previous search implementation that expired yesterday - was at 15% rollout',
    '[5]', -- UserRolloutPercentage
    'system',
    'system',
    '{"enhanced": "enhanced-search", "legacy": "old-search"}',
    'legacy',
    '{"service": "search", "type": "algorithm", "status": "expired", "rollout": "partial"}',
    15, -- Was at 15% rollout when expired
    NOW() - INTERVAL '1 day' -- Expired yesterday
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, expiration_date
) VALUES (
    'experimental-analytics',
    'Experimental Analytics Dashboard',
    'Experimental analytics feature that was being tested with select tenants - expired yesterday',
    '[1]', -- Enabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "analytics", "type": "experimental", "status": "expired", "phase": "pilot"}',
    '["pilot-tenant-1", "beta-analytics-corp", "test-organization"]',
    NOW() - INTERVAL '1 day' -- Expired yesterday
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'mobile-app-redesign-pilot',
    'Mobile App Redesign Pilot Program',
    'Mobile redesign that was targeted to specific user groups - expired yesterday',
    '[4]', -- UserTargeted
    'system',
    'system',
    '{"new": "redesigned-mobile", "old": "classic-mobile"}',
    'old',
    '{"service": "mobile", "type": "redesign", "status": "expired", "platform": "ios-android"}',
    '[
        {
            "attribute": "mobileVersion",
            "operator": 6,
            "values": ["2.0.0"],
            "variation": "new"
        },
        {
            "attribute": "betaTester",
            "operator": 0,
            "values": ["true"],
            "variation": "new"
        }
    ]',
    '["mobile-tester-001", "ui-designer-alice", "beta-user-bob", "app-developer-charlie"]',
    '["old-device-user-001", "stability-user-diana", "conservative-mobile-eve"]',
    NOW() - INTERVAL '1 day' -- Expired yesterday
) ON CONFLICT (key) DO UPDATE SET 
    targeting_rules = EXCLUDED.targeting_rules,
    enabled_users = EXCLUDED.enabled_users,
    disabled_users = EXCLUDED.disabled_users;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, 
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days, expiration_date
) VALUES (
    'weekend-flash-sale-q3',
    'Q3 Weekend Flash Sale Campaign',
    'Limited time weekend flash sale that ran during Q3 and expired yesterday',
    '[2, 3]', -- Scheduled + TimeWindow
    'system',
    'system',
    '{"sale": "flash-sale-prices", "regular": "standard-prices"}',
    'regular',
    '{"campaign": "q3-flash-sale", "type": "promotional", "status": "expired", "period": "weekend"}',
    NOW() - INTERVAL '30 days',  -- Was scheduled to start 30 days ago
    NOW() - INTERVAL '2 days',   -- Was scheduled to end 2 days ago
    '00:00:00', -- Midnight
    '23:59:59', -- End of day
    'UTC',
    '[6, 7]', -- Weekends only
    NOW() - INTERVAL '1 day' -- Expired yesterday
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- TENANT TARGETED FLAGS (evaluation_modes: [7])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, 
    enabled_tenants, disabled_tenants, expiration_date
) VALUES (
    'enterprise-analytics-suite',
    'Enterprise Analytics Suite',
    'Advanced analytics features targeted to specific enterprise tenants',
    '[7]', -- TenantTargeted
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "analytics", "type": "enterprise", "tier": "advanced", "status": "tenant-targeted"}',
    '["acme-corp", "global-industries", "tech-giants-inc", "innovation-labs", "enterprise-solutions-ltd"]',
    '["startup-company", "small-business-co", "trial-tenant", "free-tier-org"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, 
    enabled_tenants, disabled_tenants, expiration_date
) VALUES (
    'white-label-branding',
    'White Label Branding Features',
    'Custom branding and white-label features for select partners and enterprise clients',
    '[7]', -- TenantTargeted
    'system',
    'system',
    '{"custom": "white-label-enabled", "standard": "default-branding"}',
    'standard',
    '{"service": "branding", "type": "white-label", "partner": "enterprise", "status": "tenant-targeted"}',
    '["partner-alpha", "partner-beta", "white-label-corp", "custom-brand-inc", "enterprise-partner-solutions", "mega-client-xyz"]',
    '["competitor-company", "unauthorized-reseller", "blocked-partner"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, 
    enabled_tenants, disabled_tenants, expiration_date
) VALUES (
    'compliance-reporting-module',
    'Advanced Compliance Reporting',
    'SOX, GDPR, and HIPAA compliance reporting features for regulated industry tenants',
    '[7]', -- TenantTargeted
    'system',
    'system',
    '{"compliance": "full-compliance-suite", "basic": "standard-reporting"}',
    'basic',
    '{"service": "compliance", "type": "reporting", "regulation": "multi", "status": "tenant-targeted"}',
    '["healthcare-corp", "financial-services-inc", "pharma-company", "bank-holdings", "insurance-giant", "government-agency"]',
    '["non-regulated-startup", "consumer-app-company", "entertainment-corp"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- ADDITIONAL TENANT ROLLOUT PERCENTAGE FLAGS (evaluation_modes: [6])
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, tenant_percentage_enabled, expiration_date
) VALUES (
    'advanced-search-engine',
    'Advanced Search Engine Rollout',
    'Progressive rollout of enhanced search capabilities to 75% of tenants',
    '[6]', -- TenantRolloutPercentage
    'system',
    'system',
    '{"enhanced": "advanced-search", "legacy": "basic-search"}',
    'legacy',
    '{"service": "search", "type": "engine-upgrade", "performance": "enhanced", "status": "tenant-percentage"}',
    75, -- 75% of tenants
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, tenant_percentage_enabled, expiration_date
) VALUES (
    'real-time-notifications',
    'Real-time Notification System',
    'WebSocket-based real-time notifications rolled out to 45% of tenants',
    '[6]', -- TenantRolloutPercentage
    'system',
    'system',
    '{"realtime": "websocket-notifications", "polling": "traditional-polling"}',
    'polling',
    '{"service": "notifications", "type": "realtime", "transport": "websocket", "status": "tenant-percentage"}',
    45, -- 45% of tenants
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, tenant_percentage_enabled, expiration_date
) VALUES (
    'multi-region-backup',
    'Multi-Region Data Backup',
    'Automated multi-region backup system gradually rolled out to 30% of tenants',
    '[6]', -- TenantRolloutPercentage
    'system',
    'system',
    '{"multi-region": "geo-distributed-backup", "single-region": "local-backup-only"}',
    'single-region',
    '{"service": "backup", "type": "multi-region", "reliability": "high", "status": "tenant-percentage"}',
    30, -- 30% of tenants for careful rollout of critical backup feature
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, tenant_percentage_enabled, expiration_date
) VALUES (
    'performance-monitoring-v2',
    'Enhanced Performance Monitoring',
    'Next-generation performance monitoring and APM features for 85% of tenants',
    '[6]', -- TenantRolloutPercentage
    'system',
    'system',
    '{"v2": "enhanced-monitoring", "v1": "basic-monitoring"}',
    'v1',
    '{"service": "monitoring", "type": "performance", "version": "v2", "status": "tenant-percentage"}',
    85, -- 85% rollout for monitoring improvements
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, evaluation_modes, created_by, updated_by,
    variations, default_variation, tags, user_percentage_enabled, expiration_date
) VALUES (
    'checkout-version',
    'Order Processing Algorithm A/B Test',
    'A/B testing different order processing implementations - v1 (legacy proven), v2 (enhanced optimized), v3 (experimental cutting-edge)',
    '[5]', -- UserRolloutPercentage
    'system',
    'system',
    '{"v1": "v1", "v2": "v2", "v3": "v3"}',
    'v1', -- Default to proven legacy implementation
    '{"service": "orders", "type": "processing-algorithm", "component": "checkout", "criticality": "high", "test-type": "a-b-technical", "status": "user-percentage"}',
    33, -- 33% gradual rollout for new technical implementations (remaining 67% get v1)
    NOW() + INTERVAL '6 months' -- 6 month A/B test period
) ON CONFLICT (key) DO UPDATE SET 
    variations = EXCLUDED.variations,
    default_variation = EXCLUDED.default_variation,
    user_percentage_enabled = EXCLUDED.user_percentage_enabled,
    description = EXCLUDED.description,
    tags = EXCLUDED.tags;

-- Grant permissions (if needed for specific user)
-- GRANT ALL PRIVILEGES ON SCHEMA usage_demo TO propel_user;
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA usage_demo TO propel_user;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA usage_demo TO propel_user;

-- ====================================================================
-- SUMMARY OF EVALUATION MODE EXAMPLES
-- ====================================================================
-- This script includes examples for all valid evaluation_modes combinations:
-- 
-- [0]        = Disabled                                    ✓ Demonstrated
-- [1]        = Enabled                                     ✓ Demonstrated  
-- [2]        = Scheduled                                   ✓ Demonstrated
-- [3]        = TimeWindow                                  ✓ Demonstrated
-- [4]        = UserTargeted                                ✓ Demonstrated
-- [5]        = UserRolloutPercentage                       ✓ Demonstrated
-- [6]        = TenantRolloutPercentage                     ✓ Demonstrated
-- [2, 3]     = Scheduled + TimeWindow                      ✓ Demonstrated
-- [2, 4]     = Scheduled + UserTargeted                    ✓ Demonstrated
-- [2, 5]     = Scheduled + UserRolloutPercentage           ✓ Demonstrated
-- [3, 4]     = TimeWindow + UserTargeted                   ✓ Demonstrated
-- [3, 5]     = TimeWindow + UserRolloutPercentage          ✓ Demonstrated
-- [4, 5]     = UserTargeted + UserRolloutPercentage        ✓ Demonstrated
-- [2, 3, 4]  = Scheduled + TimeWindow + UserTargeted       ✓ Demonstrated
-- [2, 3, 4, 5] = All user modes combined                   ✓ Demonstrated
--
-- Note: Enabled [1] and Disabled [0] are mutually exclusive and cannot
-- be combined with other modes or each other.
--
-- Additional examples demonstrate tenant-level controls and multi-tenancy scenarios

-- ====================================================================
-- EXPIRED FEATURE FLAGS (Expiration date set to yesterday UTC)
-- ====================================================================
-- These flags demonstrate expired feature flags for testing cleanup and archival processes