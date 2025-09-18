-- ====================================================================
-- COMPREHENSIVE FEATURE FLAG EVALUATION MODE EXAMPLES
-- ====================================================================
-- These examples demonstrate all valid evaluation_modes combinations
-- Disabled = 0, Enabled = 1, Scheduled = 2, TimeWindow = 3, 
-- UserTargeted = 4, UserRolloutPercentage = 5, TenantRolloutPercentage = 6

-- Flags used in demo api that will be included in the initial database setup
-- 1. recommendation-algorithm (variations, user targeting)
-- 2. featured-products-launch (user targeting (10 allowed users by user ids)
-- 3. enhanced-catalog-ui (variations, user percentage rollout)


-- ====================================================================
-- DEMO API FEATURE FLAGS
-- ====================================================================

-- Admin Panel Access (Enabled by default with user targeting)
INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, targeting_rules, expiration_date
) VALUES (
    'admin-panel-enabled',
    'Admin Panel Access',
    'Controls access to administrative panel features including user management, system settings, and sensitive operations',
    '[1, 4]', -- Enabled + UserTargeted
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"category": "security", "impact": "high", "team": "platform", "environment": "all"}',
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
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO UPDATE SET 
    targeting_rules = EXCLUDED.targeting_rules;

-- Checkout Version (Disabled by default with percentage rollout for A/B testing)
INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, user_percentage_enabled, expiration_date
) VALUES (
    'checkout-version',
    'Checkout Processing Version',
    'Controls which checkout processing implementation is used for A/B testing. Supports v1 (legacy stable), v2 (enhanced with optimizations), and v3 (experimental cutting-edge algorithms). All variations achieve the same business outcome with different technical approaches.',
    '[5]', -- UserRolloutPercentage
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"v1": "v1", "v2": "v2", "v3": "v3"}',
    'v1',
    '{"category": "performance", "type": "a-b-test", "impact": "medium", "team": "checkout", "variations": "v1,v2,v3"}',
    33, -- 33% get v2/v3 variations
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- New Email Service (Disabled by default)
INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, expiration_date
) VALUES (
    'new-email-service',
    'New Email Service',
    'Controls whether to use the enhanced email service implementation with improved performance and features, or fall back to the legacy email service. Enables safe rollout of new email infrastructure with automatic fallback for resilience.',
    '[0]', -- Disabled
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"category": "infrastructure", "type": "implementation-toggle", "impact": "medium", "team": "platform", "rollback": "automatic"}',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- New Payment Processor (Disabled by default)
INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, expiration_date
) VALUES (
    'new-payment-processor',
    'New Payment Processor',
    'Controls whether to use the enhanced payment processing implementation with improved performance and features, or fall back to the legacy processor. Enables gradual rollout with automatic fallback for resilience and risk mitigation during payment processing.',
    '[0]', -- Disabled
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"category": "payment", "type": "implementation-toggle", "impact": "high", "team": "payments", "rollback": "automatic", "critical": "true"}',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- New Product API (Disabled by default)
INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, expiration_date
) VALUES (
    'new-product-api',
    'New Product API',
    'Controls whether to use the new enhanced product API implementation with improved performance and additional product data, or fall back to the legacy API. Enables safe rollout of API improvements without affecting existing functionality.',
    '[0]', -- Disabled
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"category": "api", "type": "implementation-toggle", "impact": "medium", "team": "product", "rollback": "instant"}',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- Featured Products Launch (Scheduled)
INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, scheduled_enable_date, scheduled_disable_date, expiration_date
) VALUES (
    'featured-products-launch',
    'Featured Products Launch',
    'Controls the scheduled launch of enhanced featured products display with new promotions and special pricing. Designed for coordinated marketing campaigns and product launches that require precise timing across all platform touchpoints.',
    '[2]', -- Scheduled
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"category": "marketing", "type": "scheduled-launch", "impact": "high", "team": "product-marketing", "coordination": "required"}',
    NOW() + INTERVAL '1 hour', -- Enable in 1 hour
    NOW() + INTERVAL '30 days', -- Disable after 30 days
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- Enhanced Catalog UI (Disabled by default)
INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, user_percentage_enabled, expiration_date
) VALUES (
    'enhanced-catalog-ui',
    'Enhanced Catalog UI',
    'Controls whether to display the enhanced catalog interface with advanced features like detailed analytics, live chat, and smart recommendations. Typically enabled during business hours when customer support is available to assist users with the more complex interface features.',
    '[5]', -- UserRolloutPercentage
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"enhanced": "enhanced-catalog", "legacy": "old-catalog"}',
    'legacy',
    '{"category": "ui", "type": "time-window", "impact": "medium", "team": "frontend", "support-dependent": "true"}',
    25, -- 25% gradual rollout
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- Recommendation Algorithm (Enabled by default with user targeting)
INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'recommendation-algorithm',
    'Recommendation Algorithm',
    'Controls which recommendation algorithm implementation is used for generating user recommendations. Supports variations including machine-learning, content-based, and collaborative-filtering algorithms. Enables A/B testing of different technical approaches while maintaining consistent business functionality.',
    '[1, 4]', -- Enabled + UserTargeted
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"collaborative-filtering": "collaborative-filtering", "content-based": "content-based", "machine-learning": "machine-learning"}',
    'collaborative-filtering',
    '{"category": "algorithm", "type": "variation-test", "impact": "medium", "team": "recommendations", "variations": "ml,content-based,collaborative-filtering", "default": "collaborative-filtering"}',
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
-- DISABLED FLAGS (evaluation_modes: [0])
-- ====================================================================

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, is_permanent, expiration_date
) VALUES (
    'api-maintenance',
    'API Maintenance Mode',
    'When enabled, API endpoints return maintenance responses - disabled by default',
    '[0]', -- Disabled
    NULL,
    NULL,
    0,
    '{"on": true, "off": false}',
    'off',
    '{"service": "api", "type": "maintenance", "status": "disabled"}',
    true,
    NOW() + INTERVAL '10 years'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- TIME WINDOW FLAGS (evaluation_modes: [3])
-- ====================================================================

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, window_start_time, window_end_time, time_zone, window_days, expiration_date
) VALUES (
    'flash-sale-window',
    'Flash Sale Time Window',
    'Shows flash sale products only during business hours (9 AM - 6 PM EST, weekdays)',
    '[3]', -- TimeWindow
    'Propel.ClientApi',
    '1.0.0.0',
    2,
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
-- TENANT ROLLOUT PERCENTAGE FLAGS (evaluation_modes: [6])
-- ====================================================================

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, tenant_percentage_enabled, expiration_date
) VALUES (
    'tenant-percentage-rollout',
    'New Dashboard Tenant Rollout',
    'Progressive rollout of new dashboard to 60% of tenants for gradual deployment',
    '[6]', -- TenantRolloutPercentage
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "rollout", "type": "percentage", "component": "dashboard", "status": "tenant-percentage"}',
    60, -- 60% of tenants
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- SCHEDULED WITH TIME WINDOW (evaluation_modes: [2, 3])
-- ====================================================================

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, 
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days, expiration_date
) VALUES (
    'holiday-promotions',
    'Holiday Promotions with Business Hours',
    'Holiday promotions active only during scheduled period and within business hours',
    '[2, 3]', -- Scheduled + TimeWindow
    'Propel.ClientApi',
    '1.0.0.0',
    2,
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
-- SCHEDULED WITH USER TARGETING (evaluation_modes: [2, 4])
-- ====================================================================

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags,
    scheduled_enable_date, scheduled_disable_date, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'beta-features-preview',
    'Scheduled Beta Features Preview',
    'Beta features available to specific user groups during preview period',
    '[2, 4]', -- Scheduled + UserTargeted
    'Propel.ClientApi',
    '1.0.0.0',
    2,
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
-- SCHEDULED WITH USER ROLLOUT PERCENTAGE (evaluation_modes: [2, 5])
-- ====================================================================

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, 
    scheduled_enable_date, scheduled_disable_date, user_percentage_enabled, expiration_date
) VALUES (
    'premium-features-rollout',
    'Scheduled Premium Features Rollout',
    'Gradual rollout of premium features starting at scheduled time',
    '[2, 5]', -- Scheduled + UserRolloutPercentage
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"premium": "premium-tier", "standard": "standard-tier"}',
    'standard',
    '{"service": "premium", "type": "scheduled-rollout", "status": "scheduled-with-user-percentage"}',
    NOW() + INTERVAL '1 day',  -- Start rollout tomorrow
    NOW() + INTERVAL '60 days', -- Complete rollout in 60 days
    40, -- Start with 40% of users
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- ====================================================================
-- TIME WINDOW WITH USER TARGETING (evaluation_modes: [3, 4])
-- ====================================================================

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags,
    window_start_time, window_end_time, time_zone, window_days, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'priority-support-hours',
    'Priority Support During Business Hours',
    'Enhanced support features for premium users during business hours',
    '[3, 4]', -- TimeWindow + UserTargeted
    NULL,
    NULL,
    0,
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
-- TIME WINDOW WITH USER ROLLOUT PERCENTAGE (evaluation_modes: [3, 5])
-- ====================================================================

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags,
    window_start_time, window_end_time, time_zone, window_days, 
    user_percentage_enabled, expiration_date
) VALUES (
    'peak-hours-optimization',
    'Peak Hours Performance Optimization',
    'Enable performance optimizations for subset of users during peak hours',
    '[3, 5]', -- TimeWindow + UserRolloutPercentage
    NULL,
    NULL,
    0,
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
-- COMPLEX COMBINATION: SCHEDULED + TIME WINDOW + USER TARGETING (evaluation_modes: [2, 3, 4])
-- ====================================================================

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags,
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'vip-event-access',
    'VIP Event Access with Limited Hours',
    'VIP users get access to special events during scheduled period and specific hours',
    '[2, 3, 4]', -- Scheduled + TimeWindow + UserTargeted
    'Propel.ClientApi',
    '1.0.0.0',
    2,
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
-- MAXIMUM COMPLEXITY: SCHEDULED + TIME WINDOW + USER TARGETING + USER PERCENTAGE (evaluation_modes: [2, 3, 4, 5])
-- ====================================================================

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
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
    'Propel.ClientApi',
    '1.0.0.0',
    2,
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
-- TENANT-SPECIFIC FEATURE FLAGS
-- ====================================================================

-- Enable tenant-specific features
INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, enabled_tenants, expiration_date
) VALUES (
    'tenant-premium-features',
    'Premium Features for Tenants',
    'Enable advanced features for premium and enterprise tenants only',
    '[1]', -- Enabled
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "premium", "type": "access-control", "tier": "premium", "status": "enabled-for-tenants"}',
    '["premium-corp", "enterprise-solutions", "vip-client-alpha", "mega-corp-beta"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

-- Tenant rollout with percentage and tenant targeting combined
INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, enabled_tenants, disabled_tenants, tenant_percentage_enabled, expiration_date
) VALUES (
    'tenant-beta-program',
    'Multi-Tenant Beta Program',
    'Beta features with tenant percentage rollout plus explicit inclusions/exclusions',
    '[6]', -- TenantRolloutPercentage
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "multi", "type": "beta-program", "phase": "phase1", "status": "tenant-percentage-with-lists"}',
    '["beta-tester-1", "beta-tester-2", "early-adopter-corp", "tech-forward-inc"]',
    '["conservative-corp", "legacy-systems-ltd", "security-first-org", "compliance-strict-co"]',
    40, -- 40% of remaining tenants (after explicit inclusions/exclusions)
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, expiration_date
) VALUES (
    'legacy-checkout-v1',
    'Legacy Checkout System V1',
    'Old checkout system that was deprecated and expired yesterday',
    '[0]', -- Disabled
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"service": "checkout", "type": "legacy", "status": "expired", "deprecated": "true"}',
    NOW() - INTERVAL '1 day' -- Expired yesterday
) ON CONFLICT (key) DO NOTHING;

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, user_percentage_enabled, expiration_date
) VALUES (
    'old-search-algorithm',
    'Deprecated Search Algorithm',
    'Previous search implementation that expired yesterday - was at 15% rollout',
    '[5]', -- UserRolloutPercentage
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"enhanced": "enhanced-search", "legacy": "old-search"}',
    'legacy',
    '{"service": "search", "type": "algorithm", "status": "expired", "rollout": "partial"}',
    15, -- Was at 15% rollout when expired
    NOW() - INTERVAL '1 day' -- Expired yesterday
) ON CONFLICT (key) DO NOTHING;

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, enabled_tenants, expiration_date
) VALUES (
    'experimental-analytics',
    'Experimental Analytics Dashboard',
    'Experimental analytics feature that was being tested with select tenants - expired yesterday',
    '[1]', -- Enabled
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"service": "analytics", "type": "experimental", "status": "expired", "phase": "pilot"}',
    '["pilot-tenant-1", "beta-analytics-corp", "test-organization"]',
    NOW() - INTERVAL '1 day' -- Expired yesterday
) ON CONFLICT (key) DO NOTHING;

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, targeting_rules, 
    enabled_users, disabled_users, expiration_date
) VALUES (
    'mobile-app-redesign-pilot',
    'Mobile App Redesign Pilot Program',
    'Mobile redesign that was targeted to specific user groups - expired yesterday',
    '[4]', -- UserTargeted
    'Propel.ClientApi',
    '1.0.0.0',
    2,
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

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, 
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days, expiration_date
) VALUES (
    'weekend-flash-sale-q3',
    'Q3 Weekend Flash Sale Campaign',
    'Limited time weekend flash sale that ran during Q3 and expired yesterday',
    '[2, 3]', -- Scheduled + TimeWindow
    'Propel.ClientApi',
    '1.0.0.0',
    2,
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

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, 
    enabled_tenants, disabled_tenants, expiration_date
) VALUES (
    'enterprise-analytics-suite',
    'Enterprise Analytics Suite',
    'Advanced analytics features targeted to specific enterprise tenants',
    '[7]', -- TenantTargeted
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"on": true, "off": false}',
    'off',
    '{"service": "analytics", "type": "enterprise", "tier": "advanced", "status": "tenant-targeted"}',
    '["acme-corp", "global-industries", "tech-giants-inc", "innovation-labs", "enterprise-solutions-ltd"]',
    '["startup-company", "small-business-co", "trial-tenant", "free-tier-org"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, 
    enabled_tenants, disabled_tenants, expiration_date
) VALUES (
    'white-label-branding',
    'White Label Branding Features',
    'Custom branding and white-label features for select partners and enterprise clients',
    '[7]', -- TenantTargeted
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"custom": "white-label-enabled", "standard": "default-branding"}',
    'standard',
    '{"service": "branding", "type": "white-label", "partner": "enterprise", "status": "tenant-targeted"}',
    '["partner-alpha", "partner-beta", "white-label-corp", "custom-brand-inc", "enterprise-partner-solutions", "mega-client-xyz"]',
    '["competitor-company", "unauthorized-reseller", "blocked-partner"]',
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, 
    enabled_tenants, disabled_tenants, expiration_date
) VALUES (
    'compliance-reporting-module',
    'Advanced Compliance Reporting',
    'SOX, GDPR, and HIPAA compliance reporting features for regulated industry tenants',
    '[7]', -- TenantTargeted
    'Propel.ClientApi',
    '1.0.0.0',
    2,
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

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, tenant_percentage_enabled, expiration_date
) VALUES (
    'advanced-search-engine',
    'Advanced Search Engine Rollout',
    'Progressive rollout of enhanced search capabilities to 75% of tenants',
    '[6]', -- TenantRolloutPercentage
    'Propel.ClientApi',
    '1.0.0.0',
    2,
    '{"enhanced": "advanced-search", "legacy": "basic-search"}',
    'legacy',
    '{"service": "search", "type": "engine-upgrade", "performance": "enhanced", "status": "tenant-percentage"}',
    75, -- 75% of tenants
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, tenant_percentage_enabled, expiration_date
) VALUES (
    'real-time-notifications',
    'Real-time Notification System',
    'WebSocket-based real-time notifications rolled out to 45% of tenants',
    '[6]', -- TenantRolloutPercentage
    NULL,
    NULL,
    0,
    '{"realtime": "websocket-notifications", "polling": "traditional-polling"}',
    'polling',
    '{"service": "notifications", "type": "realtime", "transport": "websocket", "status": "tenant-percentage"}',
    45, -- 45% of tenants
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, tenant_percentage_enabled, expiration_date
) VALUES (
    'multi-region-backup',
    'Multi-Region Data Backup',
    'Automated multi-region backup system gradually rolled out to 30% of tenants',
    '[6]', -- TenantRolloutPercentage
    NULL,
    NULL,
    0,
    '{"multi-region": "geo-distributed-backup", "single-region": "local-backup-only"}',
    'single-region',
    '{"service": "backup", "type": "multi-region", "reliability": "high", "status": "tenant-percentage"}',
    30, -- 30% of tenants for careful rollout of critical backup feature
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

INSERT INTO feature_flags (
    key, name, description, evaluation_modes,
    application_name, application_version, scope,
    variations, default_variation, tags, tenant_percentage_enabled, expiration_date
) VALUES (
    'performance-monitoring-v2',
    'Enhanced Performance Monitoring',
    'Next-generation performance monitoring and APM features for 85% of tenants',
    '[6]', -- TenantRolloutPercentage
    NULL,
    NULL,
    0,
    '{"v2": "enhanced-monitoring", "v1": "basic-monitoring"}',
    'v1',
    '{"service": "monitoring", "type": "performance", "version": "v2", "status": "tenant-percentage"}',
    85, -- 85% rollout for monitoring improvements
    NOW() + INTERVAL '1 year'
) ON CONFLICT (key) DO NOTHING;

