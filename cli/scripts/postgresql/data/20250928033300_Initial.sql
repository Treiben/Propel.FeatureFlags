-- ====================================================================
-- COMPREHENSIVE FEATURE FLAG EVALUATION MODE EXAMPLES
-- ====================================================================
-- These examples demonstrate all valid evaluation_modes combinations
--	Off = 0
--	On = 1
--	Scheduled = 2
--	TimeWindow = 3
--	UserTargeted = 4
--	UserRolloutPercentage = 5
--	TenantRolloutPercentage = 6
--	TenantTargeted = 7
--	TargetingRules = 8

-- Targeting rules use operators:
--	Equals = 0
--	NotEquals = 1
--	Contains = 2
--	NotContains = 3
--	In = 4
--	NotIn = 5
--	GreaterThan = 6
--	LessThan = 7

-- Flags used in demo api that will be included in the initial database setup
-- 1. recommendation-algorithm (variations, user targeting)
-- 2. featured-products-launch (user targeting (10 allowed users by user ids)
-- 3. enhanced-catalog-ui (variations, user percentage rollout)

SET search_path TO dashboard;

SET my.global_scope = 0;
SET my.global_name = 'global';
SET my.global_version = '0.0.0.0';

SET my.app_scope = 2;
SET my.app_name = 'WebClientDemo';
SET my.app_version = '1.0.0.0';

------------------------------------------------------------------------
SET my.flag_key = 'admin-panel-enabled';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes, default_variation, targeting_rules
) 
VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Admin Panel Access',
    'Controls access to administrative panel features including user management, system settings, and sensitive operations',
    '[8]', -- TargetingRules
	'off',
    '[
        {
            "attribute": "role",
            "operator": 4,
            "values": ["admin", "super-admin"],
            "variation": "on"
        },
        {
            "attribute": "department",
            "operator": 4,
            "values": ["engineering", "operations"],
            "variation": "on"
        }
    ]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"category": "security", "impact": "high", "team": "platform", "environment": "all"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');

------------------------------------------------------------------------
SET my.flag_key = 'checkout-version';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, user_percentage_enabled
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Checkout Processing Version',
    'Controls which checkout processing implementation is used for A/B testing. Supports v1 (legacy stable), v2 (enhanced with optimizations), and v3 (experimental cutting-edge algorithms). All variations achieve the same business outcome with different technical approaches.',
    '[5]', -- UserRolloutPercentage
    '{"v1": "v1", "v2": "v2", "v3": "v3"}',
    'v1',
    33 -- 33% get v2/v3 variations
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"category": "performance", "type": "a-b-test", "impact": "medium", "team": "checkout", "variations": "v1,v2,v3"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');

------------------------------------------------------------------------
SET my.flag_key = 'new-payment-processor';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'New Payment Processor',
    'Controls whether to use the enhanced payment processing implementation with improved performance and features, or fall back to the legacy processor. Enables gradual rollout with automatic fallback for resilience and risk mitigation during payment processing.',
    '[0]' -- Disabled
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"category": "payment", "type": "implementation-toggle", "impact": "high", "team": "payments", "rollback": "automatic", "critical": "true"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'new-product-api';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'New Product API',
    'Controls whether to use the new enhanced product API implementation with improved performance and additional product data, or fall back to the legacy API. Enables safe rollout of API improvements without affecting existing functionality.',
    '[0]' -- Disabled
)
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"category": "api", "type": "implementation-toggle", "impact": "medium", "team": "product", "rollback": "instant"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'featured-products-launch';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    scheduled_enable_date, scheduled_disable_date
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Featured Products Launch',
    'Controls the scheduled launch of enhanced featured products display with new promotions and special pricing. Designed for coordinated marketing campaigns and product launches that require precise timing across all platform touchpoints.',
    '[2]', -- Scheduled
    NOW() + INTERVAL '1 hour', -- Enable in 1 hour
    NOW() + INTERVAL '30 days' -- Disable after 30 days
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"category": "marketing", "type": "scheduled-launch", "impact": "high", "team": "product-marketing", "coordination": "required"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'enhanced-catalog-ui';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, user_percentage_enabled,
    window_start_time, window_end_time, time_zone, window_days
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Enhanced Catalog UI',
    'Controls whether to display the enhanced catalog interface with advanced features like detailed analytics, live chat, and smart recommendations. Typically enabled during business hours when customer support is available to assist users with the more complex interface features.',
    '[3, 5]', -- TimeWindow + UserRolloutPercentage
    '{"enhanced": "enhanced-catalog", "legacy": "old-catalog"}',
    'legacy',
    50, -- 50% gradual rollout
    '09:00:00', -- 9 AM
    '18:00:00', -- 6 PM
    'America/Chicago',
    '[1, 2, 3, 4, 5]' -- Monday to Friday
)
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"category": "ui", "type": "time-window", "impact": "medium", "team": "frontend", "support-dependent": "true"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'recommendation-algorithm';

INSERT INTO feature_flags (
    key, application_name, application_version, scope, 
	name, description, evaluation_modes,
    variations, default_variation, targeting_rules, 
    enabled_users, disabled_users
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Recommendation Algorithm',
    'Controls which recommendation algorithm implementation is used for generating user recommendations. Supports variations including machine-learning, content-based, and collaborative-filtering algorithms. Enables A/B testing of different technical approaches while maintaining consistent business functionality.',
    '[4, 8]', -- UserTargeted, TargetingRules
    '{"collaborative-filtering": "collaborative-filtering", "content-based": "content-based", "machine-learning": "machine-learning"}',
    'collaborative-filtering',
    '[
        {
            "attribute": "userType",
            "operator": 4,
            "values": ["premium", "enterprise"],
            "variation": "machine-learning"
        },
        {
            "attribute": "country",
            "operator": 4,
            "values": ["US", "CA", "UK"],
            "variation": "content-based"
        }
    ]',
    '["user123", "alice.johnson", "premium-user-456", "ml-tester-789", "data-scientist-001"]',
    '["blocked-user-999", "test-account-disabled", "spam-user-123", "violator-456"]'
)
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"category": "algorithm", "type": "variation-test", "impact": "medium", "team": "recommendations", "variations": "ml,content-based,collaborative-filtering", "default": "collaborative-filtering"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');

------------------------------------------------------------------------
SET my.flag_key = 'flash-sale-window';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    window_start_time, window_end_time, time_zone, window_days
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Flash Sale Time Window',
    'Shows flash sale products only during business hours (9 AM - 6 PM EST, weekdays)',
    '[3]', -- TimeWindow
    '09:00:00', -- 9 AM
    '18:00:00', -- 6 PM
    'America/New_York', -- EST timezone
    '[1, 2, 3, 4, 5]' -- Monday through Friday (1=Monday, 7=Sunday)
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"service": "products", "type": "time-window", "component": "flash-sale", "promotion": "business-hours", "status": "time-window"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'tenant-percentage-rollout';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    tenant_percentage_enabled
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'New Dashboard Tenant Rollout',
    'Progressive rollout of new dashboard to 60% of tenants for gradual deployment',
    '[6]', -- TenantRolloutPercentage
    60 -- 60% of tenants
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"tenant": "rollout", "type": "percentage", "component": "dashboard", "status": "tenant-percentage"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'holiday-promotions';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, 
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Holiday Promotions with Business Hours',
    'Holiday promotions active only during scheduled period and within business hours',
    '[2, 3]', -- Scheduled + TimeWindow
    '{"holiday": "holiday-pricing", "regular": "standard-pricing", "off": "disabled"}',
    'off',
    NOW() + INTERVAL '2 days',  -- Start holiday promotion in 2 days
    NOW() + INTERVAL '10 days', -- End holiday promotion in 10 days
    '08:00:00', -- 8 AM
    '20:00:00', -- 8 PM
    'America/New_York',
    '[1, 2, 3, 4, 5, 6]' -- Monday through Saturday
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"event": "holidays", "type": "promotional", "constraint": "business-hours", "status": "scheduled-with-time-window"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'beta-features-preview';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation,
    scheduled_enable_date, scheduled_disable_date, targeting_rules, 
    enabled_users, disabled_users
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Scheduled Beta Features Preview',
    'Beta features available to specific user groups during preview period',
    '[2, 4, 8]', -- Scheduled + UserTargeted + TargetingRules
    '{"beta": "beta-features", "standard": "regular-features"}',
    'standard',
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
    '["conservative-user-001", "stability-focused-eve", "production-only-frank", "risk-averse-grace"]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"type": "beta-preview", "audience": "targeted", "status": "scheduled-with-user-targeting"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'premium-features-rollout';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, 
    scheduled_enable_date, scheduled_disable_date, user_percentage_enabled
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Scheduled Premium Features Rollout',
    'Gradual rollout of premium features starting at scheduled time',
    '[2, 5]', -- Scheduled + UserRolloutPercentage
    '{"premium": "premium-tier", "standard": "standard-tier"}',
    'standard',
    NOW() + INTERVAL '1 day',  -- Start rollout tomorrow
    NOW() + INTERVAL '60 days', -- Complete rollout in 60 days
    40 -- Start with 40% of users
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"service": "premium", "type": "scheduled-rollout", "status": "scheduled-with-user-percentage"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'vip-event-access';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation,
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days, targeting_rules, 
    enabled_users, disabled_users
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'VIP Event Access with Limited Hours',
    'VIP users get access to special events during scheduled period and specific hours',
    '[2, 3, 4, 8]', -- Scheduled + TimeWindow + UserTargeted + TargetingRules
    '{"vip": "vip-access", "standard": "regular-access"}',
    'standard',
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
    '["banned-user-001", "policy-violator-frank", "restricted-account-grace", "underage-user-henry"]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"event": "vip-exclusive", "type": "access-control", "constraint": "scheduled-hours", "status": "scheduled-time-window-user-targeted"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'ultimate-premium-experience';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation,
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days,
    user_percentage_enabled, targeting_rules, 
    enabled_users, disabled_users
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Ultimate Premium Experience - Complete Feature Showcase',
    'The most sophisticated feature flag combining scheduling, time windows, user targeting, and percentage rollout',
    '[2, 3, 4, 5, 8]', -- Scheduled + TimeWindow + UserTargeted + UserRolloutPercentage + TargetingRules
    '{"ultimate": "premium-experience", "enhanced": "improved-features", "standard": "regular-features"}',
    'standard',
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
    '["budget-user-001", "basic-tier-eve", "trial-expired-frank", "inactive-account-grace"]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"tier": "ultimate", "complexity": "maximum", "showcase": "complete", "status": "all-modes-combined"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'tenant-premium-features';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    enabled_tenants
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Premium Features for Tenants',
    'Enable advanced features for premium and enterprise tenants only',
    '[1]', -- Enabled
    '["premium-corp", "enterprise-solutions", "vip-client-alpha", "mega-corp-beta"]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"tenant": "premium", "type": "access-control", "tier": "premium", "status": "enabled-for-tenants"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'tenant-beta-program';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    enabled_tenants, disabled_tenants, tenant_percentage_enabled
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Multi-Tenant Beta Program',
    'Beta features with tenant percentage rollout plus explicit inclusions/exclusions',
    '[6]', -- TenantRolloutPercentage
    '["beta-tester-1", "beta-tester-2", "early-adopter-corp", "tech-forward-inc"]',
    '["conservative-corp", "legacy-systems-ltd", "security-first-org", "compliance-strict-co"]',
    40 -- 40% of remaining tenants (after explicit inclusions/exclusions)
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"tenant": "multi", "type": "beta-program", "phase": "phase1", "status": "tenant-percentage-with-lists"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'legacy-checkout-v1';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Legacy Checkout System V1',
    'Old checkout system that was deprecated and expired yesterday',
    '[0]' -- Disabled
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"service": "checkout", "type": "legacy", "status": "expired", "deprecated": "true"}', NOW() - INTERVAL '1 day');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'old-search-algorithm';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, user_percentage_enabled
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Deprecated Search Algorithm',
    'Previous search implementation that expired yesterday - was at 15% rollout',
    '[5]', -- UserRolloutPercentage
    '{"enhanced": "enhanced-search", "legacy": "old-search"}',
    'legacy',
    15 -- Was at 15% rollout when expired
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"service": "search", "type": "algorithm", "status": "expired", "rollout": "partial"}', NOW() - INTERVAL '1 day');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'experimental-analytics';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    enabled_tenants
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Experimental Analytics Dashboard',
    'Experimental analytics feature that was being tested with select tenants - expired yesterday',
    '[7]', -- TenantTargeted
    '["pilot-tenant-1", "beta-analytics-corp", "test-organization"]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"service": "analytics", "type": "experimental", "status": "expired", "phase": "pilot"}', NOW() - INTERVAL '1 day');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'mobile-app-redesign-pilot';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, targeting_rules, 
    enabled_users, disabled_users
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Mobile App Redesign Pilot Program',
    'Mobile redesign that was targeted to specific user groups - expired yesterday',
    '[4, 8]', -- UserTargeted + TargetingRules
    '{"new": "redesigned-mobile", "old": "classic-mobile"}',
    'old',
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
    '["old-device-user-001", "stability-user-diana", "conservative-mobile-eve"]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"service": "mobile", "type": "redesign", "status": "expired", "platform": "ios-android"}', NOW() - INTERVAL '1 day');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'weekend-flash-sale-q3';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, 
    scheduled_enable_date, scheduled_disable_date,
    window_start_time, window_end_time, time_zone, window_days
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Q3 Weekend Flash Sale Campaign',
    'Limited time weekend flash sale that ran during Q3 and expired yesterday',
    '[2, 3]', -- Scheduled + TimeWindow
    '{"sale": "flash-sale-prices", "regular": "standard-prices"}',
    'regular',
    NOW() - INTERVAL '30 days',  -- Was scheduled to start 30 days ago
    NOW() - INTERVAL '2 days',   -- Was scheduled to end 2 days ago
    '00:00:00', -- Midnight
    '23:59:59', -- End of day
    'UTC',
    '[5, 6]' -- Weekends only
)
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"campaign": "q3-flash-sale", "type": "promotional", "status": "expired", "period": "weekend"}', NOW() - INTERVAL '1 day');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'enterprise-analytics-suite';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    enabled_tenants, disabled_tenants
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Enterprise Analytics Suite',
    'Advanced analytics features targeted to specific enterprise tenants',
    '[7]', -- TenantTargeted
    '["acme-corp", "global-industries", "tech-giants-inc", "innovation-labs", "enterprise-solutions-ltd"]',
    '["startup-company", "small-business-co", "trial-tenant", "free-tier-org"]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"service": "analytics", "type": "enterprise", "tier": "advanced", "status": "tenant-targeted"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'white-label-branding';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, 
    enabled_tenants, disabled_tenants
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'White Label Branding Features',
    'Custom branding and white-label features for select partners and enterprise clients',
    '[7]', -- TenantTargeted
    '{"custom": "white-label-enabled", "standard": "default-branding"}',
    'standard',
    '["partner-alpha", "partner-beta", "white-label-corp", "custom-brand-inc", "enterprise-partner-solutions", "mega-client-xyz"]',
    '["competitor-company", "unauthorized-reseller", "blocked-partner"]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"service": "branding", "type": "white-label", "partner": "enterprise", "status": "tenant-targeted"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'compliance-reporting-module';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, 
    enabled_tenants, disabled_tenants
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Advanced Compliance Reporting',
    'SOX, GDPR, and HIPAA compliance reporting features for regulated industry tenants',
    '[7]', -- TenantTargeted
    '{"compliance": "full-compliance-suite", "basic": "standard-reporting"}',
    'basic',
    '["healthcare-corp", "financial-services-inc", "pharma-company", "bank-holdings", "insurance-giant", "government-agency"]',
    '["non-regulated-startup", "consumer-app-company", "entertainment-corp"]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"service": "compliance", "type": "reporting", "regulation": "multi", "status": "tenant-targeted"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'advanced-search-engine';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, tenant_percentage_enabled
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.app_name'),
	current_setting('my.app_version'),
	current_setting('my.app_scope')::INTEGER,
    'Advanced Search Engine Rollout',
    'Progressive rollout of enhanced search capabilities to 75% of tenants',
    '[6]', -- TenantRolloutPercentage
    '{"enhanced": "advanced-search", "legacy": "basic-search"}',
    'legacy',
    75 -- 75% of tenants
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, application_name, application_version, tags, expiration_date)
VALUES (current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), '{"service": "search", "type": "engine-upgrade", "performance": "enhanced", "status": "tenant-percentage"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, application_name, application_version, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), current_setting('my.app_name'), current_setting('my.app_version'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');

------------------------------------------------------------------------
-- global flags

------------------------------------------------------------------------
SET my.flag_key = 'real-time-notifications';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, tenant_percentage_enabled
) VALUES (
    current_setting('my.flag_key'), 
	current_setting('my.global_name'),
	current_setting('my.global_version'),
	current_setting('my.global_scope')::INTEGER,
    'Real-time Notification System',
    'WebSocket-based real-time notifications rolled out to 45% of tenants',
    '[6]', -- TenantRolloutPercentage
    '{"realtime": "websocket-notifications", "polling": "traditional-polling"}',
    'polling',
    45 -- 45% of tenants
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, tags, expiration_date)
VALUES (current_setting('my.flag_key'), '{"service": "notifications", "type": "realtime", "transport": "websocket", "status": "tenant-percentage"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');

------------------------------------------------------------------------
SET my.flag_key = 'multi-region-backup';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, tenant_percentage_enabled
) VALUES (
    current_setting('my.flag_key'), 
	current_setting('my.global_name'),
	current_setting('my.global_version'),
	current_setting('my.global_scope')::INTEGER,
    'Multi-Region Data Backup',
    'Automated multi-region backup system gradually rolled out to 30% of tenants',
    '[6]', -- TenantRolloutPercentage
    '{"multi-region": "geo-distributed-backup", "single-region": "local-backup-only"}',
    'single-region',
    30 -- 30% of tenants for careful rollout of critical backup feature
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, tags, expiration_date)
VALUES (current_setting('my.flag_key'), '{"service": "backup", "type": "multi-region", "reliability": "high", "status": "tenant-percentage"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');

------------------------------------------------------------------------
SET my.flag_key = 'performance-monitoring-v2';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation, tenant_percentage_enabled
) VALUES (
    current_setting('my.flag_key'),
	current_setting('my.global_name'),
	current_setting('my.global_version'),
	current_setting('my.global_scope')::INTEGER,
    'Enhanced Performance Monitoring',
    'Next-generation performance monitoring and APM features for 85% of tenants',
    '[6]', -- TenantRolloutPercentage
    '{"v2": "enhanced-monitoring", "v1": "basic-monitoring"}',
    'v1',
    85 -- 85% rollout for monitoring improvements
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, tags, expiration_date)
VALUES (current_setting('my.flag_key'), '{"service": "monitoring", "type": "performance", "version": "v2", "status": "tenant-percentage"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'),  'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');

------------------------------------------------------------------------
SET my.flag_key = 'priority-support-hours';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation,
    window_start_time, window_end_time, time_zone, window_days, targeting_rules, 
    enabled_users, disabled_users
) VALUES (
    current_setting('my.flag_key'), 
	current_setting('my.global_name'),
	current_setting('my.global_version'),
	current_setting('my.global_scope')::INTEGER,
    'Priority Support During Business Hours',
    'Enhanced support features for premium users during business hours',
    '[3, 4]', -- TimeWindow + UserTargeted + TargetingRules
    '{"priority": "priority-support", "standard": "regular-support"}',
    'standard',
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
    '["basic-user-001", "free-tier-eve", "trial-account-frank", "suspended-user-grace"]'
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, tags, expiration_date)
VALUES (current_setting('my.flag_key'), '{"service": "support", "type": "priority", "constraint": "business-hours", "status": "time-window-with-user-targeting"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');
------------------------------------------------------------------------
SET my.flag_key = 'peak-hours-optimization';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes,
    variations, default_variation,
    window_start_time, window_end_time, time_zone, window_days, 
    user_percentage_enabled
) VALUES (
    current_setting('my.flag_key'), 
	current_setting('my.global_name'),
	current_setting('my.global_version'),
	current_setting('my.global_scope')::INTEGER,
    'Peak Hours Performance Optimization',
    'Enable performance optimizations for subset of users during peak hours',
    '[3, 5]', -- TimeWindow + UserRolloutPercentage
    '{"optimized": "performance-mode", "standard": "normal-mode"}',
    'standard',
    '10:00:00', -- 10 AM
    '14:00:00', -- 2 PM
    'America/New_York',
    '[1, 2, 3, 4, 5]', -- Weekdays only
    60 -- 60% of users during peak hours
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, tags, expiration_date)
VALUES (current_setting('my.flag_key'), '{"service": "performance", "type": "optimization", "constraint": "peak-hours", "status": "time-window-with-user-percentage"}', NOW() + INTERVAL '1 year');

INSERT INTO feature_flags_audit (flag_key, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');

------------------------------------------------------------------------
SET my.flag_key = 'api-maintenance';

INSERT INTO feature_flags (
    key, application_name, application_version, scope,
	name, description, evaluation_modes
) VALUES (
    current_setting('my.flag_key'), 
	current_setting('my.global_name'),
	current_setting('my.global_version'),
	current_setting('my.global_scope')::INTEGER,
    'API Maintenance Mode',
    'When enabled, API endpoints return maintenance responses - disabled by default',
    '[0]' -- Disabled
) 
ON CONFLICT (key, application_name, application_version)
DO UPDATE SET
	name = excluded.name,
	description = excluded.description,
	evaluation_modes = excluded.evaluation_modes,
	window_start_time = excluded.window_start_time, 
	window_end_time = excluded.window_end_time, 
	time_zone = excluded.time_zone, 
	window_days = excluded.window_days, 
	scheduled_enable_date = excluded.scheduled_enable_date, 
	scheduled_disable_date = excluded.scheduled_disable_date,
	tenant_percentage_enabled = excluded.tenant_percentage_enabled,
	enabled_tenants = excluded.enabled_tenants,
	disabled_tenants = excluded.disabled_tenants,
	user_percentage_enabled = excluded.user_percentage_enabled,
	enabled_users = excluded.enabled_users,
	disabled_users = excluded.disabled_users,
	targeting_rules = excluded.targeting_rules,
	variations = excluded.variations,
	default_variation = excluded.default_variation;

INSERT INTO feature_flags_metadata (flag_key, tags, is_permanent, expiration_date)
VALUES (current_setting('my.flag_key'), '{"service": "api", "type": "maintenance", "status": "disabled"}', true, NOW() + INTERVAL '10 years');

INSERT INTO feature_flags_audit (flag_key, action, actor, timestamp, notes)
VALUES(current_setting('my.flag_key'), 'flag-created', current_setting('my.app_name'), NOW(), 'Initial creation from seed script');