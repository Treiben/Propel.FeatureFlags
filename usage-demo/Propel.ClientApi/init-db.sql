-- ====================================================================
-- PROPEL FEATURE FLAGS - POSTGRESQL DATABASE INITIALIZATION SCRIPT
-- ====================================================================
-- This script demonstrates comprehensive feature flag configurations
-- covering all expanded FeatureFlagStatus enum values and their combinations.
-- Updated to accommodate the full range of feature flag capabilities.

-- Create the feature flags schema if it doesn't exist
CREATE SCHEMA IF NOT EXISTS usage_demo;

-- Create UUID extension for the audit table
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create the feature_flags table
CREATE TABLE IF NOT EXISTS usage_demo.feature_flags (
    key VARCHAR(255) PRIMARY KEY,
    name VARCHAR(500) NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    status INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255) NOT NULL,
    updated_by VARCHAR(255) NOT NULL,
    
    -- Expiration
    expiration_date TIMESTAMP WITH TIME ZONE NULL,
    
    -- Scheduling
    scheduled_enable_date TIMESTAMP WITH TIME ZONE NULL,
    scheduled_disable_date TIMESTAMP WITH TIME ZONE NULL,
    
    -- Time Windows
    window_start_time TIME NULL,
    window_end_time TIME NULL,
    time_zone VARCHAR(100) NULL,
    window_days JSONB NOT NULL DEFAULT '[]',
    
    -- Percentage rollout
    percentage_enabled INTEGER NOT NULL DEFAULT 0 CHECK (percentage_enabled >= 0 AND percentage_enabled <= 100),
    
    -- Targeting
    targeting_rules JSONB NOT NULL DEFAULT '[]',
    enabled_users JSONB NOT NULL DEFAULT '[]',
    disabled_users JSONB NOT NULL DEFAULT '[]',
    
    -- Tenant-level controls
    enabled_tenants JSONB NOT NULL DEFAULT '[]',
    disabled_tenants JSONB NOT NULL DEFAULT '[]',
    tenant_percentage_enabled INTEGER NOT NULL DEFAULT 0 CHECK (tenant_percentage_enabled >= 0 AND tenant_percentage_enabled <= 100),
    
    -- Variations
    variations JSONB NOT NULL DEFAULT '{}',
    default_variation VARCHAR(255) NOT NULL DEFAULT 'off',
    
    -- Metadata
    tags JSONB NOT NULL DEFAULT '{}',
    is_permanent BOOLEAN NOT NULL DEFAULT FALSE
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
CREATE INDEX IF NOT EXISTS idx_feature_flags_status ON usage_demo.feature_flags (status);
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
-- COMPREHENSIVE FEATURE FLAG STATUS EXAMPLES
-- ====================================================================
-- These examples demonstrate all FeatureFlagStatus enum values (0-23)
-- showcasing real-world use cases for each status type and combination.

-- ====================================================================
-- STATUS 0: DISABLED FLAGS
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags
) VALUES (
    'new-email-service',
    'New Email Service',
    'Use the new email service implementation instead of legacy - currently disabled',
    0, -- Disabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "notifications", "type": "implementation", "status": "disabled"}'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, is_permanent
) VALUES (
    'api-maintenance',
    'API Maintenance Mode',
    'When enabled, API endpoints return maintenance responses - disabled by default',
    0, -- Disabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "api", "type": "maintenance", "status": "disabled"}',
    true
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- STATUS 1: ENABLED FLAGS
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags
) VALUES (
    'new-product-api',
    'New Product API',
    'Enable the new product API with enhanced product listings - fully enabled',
    1, -- Enabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "products", "type": "implementation", "component": "api", "status": "enabled"}'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- STATUS 2: SCHEDULED FLAGS
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, scheduled_enable_date, scheduled_disable_date
) VALUES (
    'featured-products-launch',
    'Featured Products Launch',
    'Scheduled launch of new featured products with special promotion',
    2, -- Scheduled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "products", "type": "scheduled-launch", "component": "featured", "campaign": "q4-launch", "status": "scheduled"}',
    NOW() + INTERVAL '1 hour', -- Enable in 1 hour
    NOW() + INTERVAL '30 days' -- Disable after 30 days
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, scheduled_enable_date, scheduled_disable_date
) VALUES (
    'black-friday-mode',
    'Black Friday Special Features',
    'Enable special Black Friday features and pricing during the promotion period',
    2, -- Scheduled
    'system',
    'system',
    '{"standard": "regular-pricing", "blackfriday": "special-pricing", "off": "disabled"}',
    'off',
    '{"event": "black-friday", "type": "promotional", "status": "scheduled"}',
    '2024-11-29 00:00:00+00', -- Black Friday start
    '2024-11-30 23:59:59+00'  -- Black Friday end
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- STATUS 3: TIME WINDOW FLAGS
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, window_start_time, window_end_time, time_zone, window_days
) VALUES (
    'flash-sale-window',
    'Flash Sale Time Window',
    'Shows flash sale products only during business hours (9 AM - 6 PM EST, weekdays)',
    3, -- TimeWindow
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "products", "type": "time-window", "component": "flash-sale", "promotion": "business-hours", "status": "time-window"}',
    '09:00:00', -- 9 AM
    '18:00:00', -- 6 PM
    'America/New_York', -- EST timezone
    '[1, 2, 3, 4, 5]' -- Monday through Friday (1=Monday, 7=Sunday)
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, window_start_time, window_end_time, time_zone, window_days
) VALUES (
    'enhanced-catalog-ui',
    'Enhanced Catalog UI',
    'Shows enhanced catalog with additional features during business hours when support team is available',
    3, -- TimeWindow
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "products", "type": "time-window", "component": "catalog-ui", "reason": "support-availability", "status": "time-window"}',
    '09:00:00', -- 9 AM
    '18:00:00', -- 6 PM
    'America/New_York', -- EST timezone
    '[1, 2, 3, 4, 5]' -- Monday through Friday (1=Monday, 7=Sunday)
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- STATUS 5: SCHEDULED WITH TIME WINDOW (Scheduled = 2, TimeWindow = 3, Combined = 5)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, 
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days
) VALUES (
    'holiday-promotions',
    'Holiday Promotions with Business Hours',
    'Holiday promotions active only during scheduled period and within business hours',
    5, -- ScheduledWithTimeWindow
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
    '[1, 2, 3, 4, 5, 6]' -- Monday through Saturday
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- STATUS 6: PERCENTAGE FLAGS
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, percentage_enabled
) VALUES (
    'checkout-version',
    'Checkout Version A/B Test',
    'A/B test for different checkout flow versions (v1=legacy, v2=new, v3=experimental)',
    6, -- Percentage
    'system',
    'system',
    '{"v1": "v1", "v2": "v2", "v3": "v3"}',
    'v1',
    '{"service": "orders", "type": "ab-test", "component": "checkout", "status": "percentage"}',
    33 -- 33% of users will get the new experience
) ON CONFLICT (key) DO NOTHING;

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, percentage_enabled
) VALUES (
    'new-dashboard-ui',
    'New Dashboard UI Rollout',
    'Progressive rollout of the new dashboard interface to users',
    6, -- Percentage
    'system',
    'system',
    '{"new": "new-dashboard", "legacy": "old-dashboard"}',
    'legacy',
    '{"service": "dashboard", "type": "ui-rollout", "status": "percentage"}',
    25 -- 25% gradual rollout
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- STATUS 8: SCHEDULED WITH PERCENTAGE (Scheduled = 2, Percentage = 6, Combined = 8)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, 
    scheduled_enable_date, scheduled_disable_date, percentage_enabled
) VALUES (
    'premium-features-rollout',
    'Scheduled Premium Features Rollout',
    'Gradual rollout of premium features starting at scheduled time',
    8, -- ScheduledWithPercentage
    'system',
    'system',
    '{"premium": "premium-tier", "standard": "standard-tier"}',
    'standard',
    '{"service": "premium", "type": "scheduled-rollout", "status": "scheduled-with-percentage"}',
    NOW() + INTERVAL '1 day',  -- Start rollout tomorrow
    NOW() + INTERVAL '60 days', -- Complete rollout in 60 days
    40 -- Start with 40% of users
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- STATUS 9: TIME WINDOW WITH PERCENTAGE (TimeWindow = 3, Percentage = 6, Combined = 9)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags,
    window_start_time, window_end_time, time_zone, window_days, percentage_enabled
) VALUES (
    'peak-hours-optimization',
    'Peak Hours Performance Optimization',
    'Enable performance optimizations for subset of users during peak hours',
    9, -- TimeWindowWithPercentage
    'system',
    'system',
    '{"optimized": "performance-mode", "standard": "normal-mode"}',
    'standard',
    '{"service": "performance", "type": "optimization", "constraint": "peak-hours", "status": "time-window-with-percentage"}',
    '10:00:00', -- 10 AM
    '14:00:00', -- 2 PM
    'America/New_York',
    '[1, 2, 3, 4, 5]', -- Weekdays only
    60 -- 60% of users during peak hours
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- STATUS 11: SCHEDULED WITH TIME WINDOW AND PERCENTAGE (2 + 3 + 6 = 11)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags,
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days, percentage_enabled
) VALUES (
    'summer-campaign-limited',
    'Limited Summer Campaign',
    'Summer promotional features for subset of users during business hours and campaign period',
    11, -- ScheduledWithTimeWindowAndPercentage
    'system',
    'system',
    '{"summer": "summer-specials", "regular": "standard-offers"}',
    'regular',
    '{"campaign": "summer", "type": "promotional", "constraint": "business-hours-percentage", "status": "scheduled-with-time-window-and-percentage"}',
    NOW() + INTERVAL '7 days',  -- Start summer campaign in a week
    NOW() + INTERVAL '90 days', -- End in 90 days
    '09:00:00', -- 9 AM
    '17:00:00', -- 5 PM
    'America/New_York',
    '[1, 2, 3, 4, 5, 6, 7]', -- All days during campaign
    35 -- 35% of users get special features
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- STATUS 12: USER TARGETED FLAGS
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, targeting_rules
) VALUES (
    'recommendation-algorithm',
    'Recommendation Algorithm Selection',
    'Choose which recommendation algorithm to use (collaborative-filtering=default, content-based=similarity, machine-learning=AI)',
    12, -- UserTargeted
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
    ]'
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, targeting_rules
) VALUES (
    'admin-panel-enabled',
    'Admin Panel Access',
    'Controls access to sensitive admin operations and panel features',
    12, -- UserTargeted
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
    ]'
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

-- ====================================================================
-- STATUS 14: SCHEDULED WITH USER TARGETING (Scheduled = 2, UserTargeted = 12, Combined = 14)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags,
    scheduled_enable_date, scheduled_disable_date, targeting_rules
) VALUES (
    'beta-features-preview',
    'Scheduled Beta Features Preview',
    'Beta features available to specific user groups during preview period',
    14, -- ScheduledWithUserTargeting
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
    ]'
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

-- ====================================================================
-- STATUS 15: TIME WINDOW WITH USER TARGETING (TimeWindow = 3, UserTargeted = 12, Combined = 15)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags,
    window_start_time, window_end_time, time_zone, window_days, targeting_rules
) VALUES (
    'priority-support-hours',
    'Priority Support During Business Hours',
    'Enhanced support features for premium users during business hours',
    15, -- TimeWindowWithUserTargeting
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
    ]'
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

-- ====================================================================
-- STATUS 17: SCHEDULED WITH TIME WINDOW AND USER TARGETING (2 + 3 + 12 = 17)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags,
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days, targeting_rules
) VALUES (
    'vip-event-access',
    'VIP Event Access with Limited Hours',
    'VIP users get access to special events during scheduled period and specific hours',
    17, -- ScheduledWithTimeWindowAndUserTargeting
    'system',
    'system',
    '{"vip": "vip-access", "standard": "regular-access"}',
    'standard',
    '{"event": "vip-exclusive", "type": "access-control", "constraint": "scheduled-hours", "status": "scheduled-with-time-window-and-user-targeting"}',
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
    ]'
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

-- ====================================================================
-- STATUS 18: PERCENTAGE WITH USER TARGETING (Percentage = 6, UserTargeted = 12, Combined = 18)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, targeting_rules, percentage_enabled
) VALUES (
    'new-payment-processor',
    'New Payment Processor Integration',
    'Progressive rollout of enhanced payment processing system with automatic fallback to legacy processor',
    18, -- PercentageWithUserTargeting
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "payments", "type": "processor-migration", "component": "service-layer", "criticality": "high", "status": "percentage-with-user-targeting"}',
    '[
        {
            "attribute": "riskScore",
            "operator": 7,
            "values": ["0.3"],
            "variation": "on"
        },
        {
            "attribute": "amount",
            "operator": 7,
            "values": ["100"],
            "variation": "on"
        },
        {
            "attribute": "country",
            "operator": 0,
            "values": ["US", "CA"],
            "variation": "on"
        }
    ]',
    25 -- Conservative 25% rollout for critical payment infrastructure
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

-- ====================================================================
-- STATUS 20: SCHEDULED WITH PERCENTAGE AND USER TARGETING (2 + 6 + 12 = 20)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags,
    scheduled_enable_date, scheduled_disable_date, percentage_enabled, targeting_rules
) VALUES (
    'loyalty-program-launch',
    'Scheduled Loyalty Program Launch',
    'New loyalty program gradually rolled out to targeted customer segments',
    20, -- ScheduledWithPercentageAndUserTargeting
    'system',
    'system',
    '{"loyalty": "loyalty-enabled", "standard": "no-loyalty"}',
    'standard',
    '{"program": "loyalty", "type": "customer-retention", "rollout": "gradual", "status": "scheduled-with-percentage-and-user-targeting"}',
    NOW() + INTERVAL '14 days', -- Launch in 2 weeks
    NOW() + INTERVAL '180 days', -- Run for 6 months
    30, -- Start with 30% of eligible users
    '[
        {
            "attribute": "customerTier",
            "operator": 0,
            "values": ["silver", "gold", "platinum"],
            "variation": "loyalty"
        },
        {
            "attribute": "purchaseHistory",
            "operator": 7,
            "values": ["500"],
            "variation": "loyalty"
        }
    ]'
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

-- ====================================================================
-- STATUS 21: TIME WINDOW WITH PERCENTAGE AND USER TARGETING (3 + 6 + 12 = 21)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags,
    window_start_time, window_end_time, time_zone, window_days, 
    percentage_enabled, targeting_rules
) VALUES (
    'peak-performance-boost',
    'Peak Hours Performance Boost for Power Users',
    'Enhanced performance features for subset of power users during peak traffic hours',
    21, -- TimeWindowWithPercentageAndUserTargeting
    'system',
    'system',
    '{"boosted": "performance-enhanced", "standard": "normal-performance"}',
    'standard',
    '{"service": "performance", "type": "optimization", "audience": "power-users", "constraint": "peak-hours", "status": "time-window-with-percentage-and-user-targeting"}',
    '11:00:00', -- 11 AM
    '15:00:00', -- 3 PM
    'America/New_York',
    '[1, 2, 3, 4, 5]', -- Weekdays during peak hours
    50, -- 50% of eligible power users
    '[
        {
            "attribute": "userType",
            "operator": 0,
            "values": ["power-user", "heavy-user"],
            "variation": "boosted"
        },
        {
            "attribute": "usageLevel",
            "operator": 7,
            "values": ["1000"],
            "variation": "boosted"
        }
    ]'
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

-- ====================================================================
-- STATUS 23: COMPLETE COMBINATION (Scheduled + TimeWindow + Percentage + UserTargeting = 2 + 3 + 6 + 12 = 23)
-- ====================================================================

INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags,
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days,
    percentage_enabled, targeting_rules
) VALUES (
    'ultimate-premium-experience',
    'Ultimate Premium Experience - Complete Feature Showcase',
    'The most sophisticated feature flag combining all controls: scheduled activation, business hours only, percentage rollout, and user targeting',
    23, -- ScheduledWithTimeWindowAndPercentageAndUserTargeting
    'system',
    'system',
    '{"ultimate": "premium-experience", "enhanced": "improved-features", "standard": "regular-features"}',
    'standard',
    '{"tier": "ultimate", "complexity": "maximum", "showcase": "complete", "status": "scheduled-with-time-window-and-percentage-and-user-targeting"}',
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
    ]'
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

-- ====================================================================
-- TENANT-SPECIFIC FEATURE FLAGS FOR COMPREHENSIVE TESTING SCENARIOS
-- ====================================================================

-- Insert tenant-premium-features flag for premium tenant access
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants
) VALUES (
    'tenant-premium-features',
    'Premium Features for Tenants',
    'Enable advanced features for premium and enterprise tenants only',
    1, -- Enabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "premium", "type": "access-control", "tier": "premium", "status": "enabled-for-tenants"}',
    '["premium-corp", "enterprise-solutions", "vip-client-alpha", "mega-corp-beta"]'
) ON CONFLICT (key) DO NOTHING;

-- Insert tenant-beta-program flag for beta testing with specific tenants
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, disabled_tenants
) VALUES (
    'tenant-beta-program',
    'Multi-Tenant Beta Program',
    'Beta features enabled for select tenants, explicitly disabled for others',
    1, -- Enabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "multi", "type": "beta-program", "phase": "phase1", "status": "tenant-selective"}',
    '["beta-tester-1", "beta-tester-2", "early-adopter-corp", "tech-forward-inc"]',
    '["conservative-corp", "legacy-systems-ltd", "security-first-org", "compliance-strict-co"]'
) ON CONFLICT (key) DO NOTHING;

-- Insert tenant-percentage-rollout flag for gradual tenant rollout
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, tenant_percentage_enabled
) VALUES (
    'tenant-percentage-rollout',
    'New Dashboard Tenant Rollout',
    'Progressive rollout of new dashboard to 60% of tenants for gradual deployment',
    1, -- Enabled (but controlled by tenant percentage)
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "rollout", "type": "percentage", "component": "dashboard", "status": "tenant-percentage"}',
    60
) ON CONFLICT (key) DO NOTHING;

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

-- Grant permissions (if needed for specific user)
-- GRANT ALL PRIVILEGES ON SCHEMA usage_demo TO propel_user;
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA usage_demo TO propel_user;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA usage_demo TO propel_user;

-- ====================================================================
-- SUMMARY OF FEATURE FLAG STATUS EXAMPLES
-- ====================================================================
-- This script includes examples for all FeatureFlagStatus enum values:
-- 
-- 0  = Disabled                                    ✓ Demonstrated
-- 1  = Enabled                                     ✓ Demonstrated  
-- 2  = Scheduled                                   ✓ Demonstrated
-- 3  = TimeWindow                                  ✓ Demonstrated
-- 5  = ScheduledWithTimeWindow                     ✓ Demonstrated
-- 6  = Percentage                                  ✓ Demonstrated
-- 8  = ScheduledWithPercentage                     ✓ Demonstrated
-- 9  = TimeWindowWithPercentage                    ✓ Demonstrated
-- 11 = ScheduledWithTimeWindowAndPercentage        ✓ Demonstrated
-- 12 = UserTargeted                                ✓ Demonstrated
-- 14 = ScheduledWithUserTargeting                  ✓ Demonstrated
-- 15 = TimeWindowWithUserTargeting                 ✓ Demonstrated
-- 17 = ScheduledWithTimeWindowAndUserTargeting     ✓ Demonstrated
-- 18 = PercentageWithUserTargeting                 ✓ Demonstrated
-- 20 = ScheduledWithPercentageAndUserTargeting     ✓ Demonstrated
-- 21 = TimeWindowWithPercentageAndUserTargeting    ✓ Demonstrated
-- 23 = ScheduledWithTimeWindowAndPercentageAndUserTargeting ✓ Demonstrated
--
-- Additional examples demonstrate tenant-level controls and multi-tenancy scenarios