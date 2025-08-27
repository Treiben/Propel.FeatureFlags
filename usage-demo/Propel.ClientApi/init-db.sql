-- Database initialization script
-- This file will be executed when the PostgreSQL container starts for the first time

-- Create the feature flags schema if it doesn't exist
CREATE SCHEMA IF NOT EXISTS usage_demo;

-- Create UUID extension for the audit table
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create the feature_flags table
CREATE TABLE IF NOT EXISTS usage_demo.feature_flags (
    key VARCHAR(100) PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    status INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100) NOT NULL,
    updated_by VARCHAR(100) NOT NULL,
    
    -- Expiration
    expiration_date TIMESTAMP WITH TIME ZONE NULL,
    
    -- Scheduling
    scheduled_enable_date TIMESTAMP WITH TIME ZONE NULL,
    scheduled_disable_date TIMESTAMP WITH TIME ZONE NULL,
    
    -- Time Windows
    window_start_time TIME NULL,
    window_end_time TIME NULL,
    time_zone VARCHAR(50) NULL,
    window_days JSONB NULL,
    
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
    default_variation VARCHAR(50) NOT NULL DEFAULT 'off',
    
    -- Metadata
    tags JSONB NOT NULL DEFAULT '{}',
    is_permanent BOOLEAN NOT NULL DEFAULT FALSE
);

-- Create the feature_flag_audit table
CREATE TABLE IF NOT EXISTS usage_demo.feature_flag_audit (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    flag_key VARCHAR(100) NOT NULL,
    action VARCHAR(50) NOT NULL,
    changed_by VARCHAR(100) NOT NULL,
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

-- Insert sample feature flag for the email service
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags
) VALUES (
    'new-email-service',
    'New Email Service',
    'Use the new email service implementation instead of legacy',
    0, -- Disabled by default
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "notifications", "type": "implementation"}'
) ON CONFLICT (key) DO NOTHING;

-- Insert api-maintenance flag (disabled)
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, is_permanent
) VALUES (
    'api-maintenance',
    'API Maintenance Mode',
    'When enabled, API endpoints return maintenance responses',
    0, -- Disabled by default
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "api", "type": "maintenance"}',
    true
) ON CONFLICT (key) DO NOTHING;

-- Insert checkout-version flag for A/B testing
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, percentage_enabled
) VALUES (
    'checkout-version',
    'Checkout Version A/B Test',
    'A/B test for different checkout flow versions (v1=legacy, v2=new, v3=experimental)',
    5, -- Percentage rollout
    'system',
    'system',
    '{"v1": "v1", "v2": "v2", "v3": "v3"}',
    'v1',
    '{"service": "orders", "type": "ab-test", "component": "checkout"}',
    33 -- 33% of users will get the new experience
) ON CONFLICT (key) DO NOTHING;

-- Insert new-product-api flag
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags
) VALUES (
    'new-product-api',
    'New Product API',
    'Enable the new product API with enhanced product listings',
    0, -- Disabled by default
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "products", "type": "implementation", "component": "api"}'
) ON CONFLICT (key) DO NOTHING;

-- Insert recommendation-algorithm flag for algorithm selection
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, targeting_rules
) VALUES (
    'recommendation-algorithm',
    'Recommendation Algorithm Selection',
    'Choose which recommendation algorithm to use (collaborative-filtering=default, content-based=similarity, machine-learning=AI)',
    4, -- User targeted
    'system',
    'system',
    '{"collaborative-filtering": "collaborative-filtering", "content-based": "content-based", "machine-learning": "machine-learning"}',
    'collaborative-filtering',
    '{"service": "recommendations", "type": "algorithm", "component": "engine"}',
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

-- Insert admin-panel-enabled flag for admin access control
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, targeting_rules
) VALUES (
    'admin-panel-enabled',
    'Admin Panel Access',
    'Controls access to sensitive admin operations and panel features',
    4, -- User targeted
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "admin", "type": "access-control", "component": "panel", "security": "high"}',
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

-- Insert featured-products-launch flag for scheduled rollout
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
    '{"service": "products", "type": "scheduled-launch", "component": "featured", "campaign": "q4-launch"}',
    NOW() + INTERVAL '1 hour', -- Enable in 1 hour
    NOW() + INTERVAL '30 days' -- Disable after 30 days
) ON CONFLICT (key) DO NOTHING;

-- Insert flash-sale-window flag for time window demonstration
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, window_start_time, window_end_time, time_zone, window_days
) VALUES (
    'flash-sale-window',
    'Flash Sale Time Window',
    'Shows flash sale products only during business hours (9 AM - 6 PM EST, weekdays)',
    3, -- Time window
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "products", "type": "time-window", "component": "flash-sale", "promotion": "business-hours"}',
    '09:00:00', -- 9 AM
    '18:00:00', -- 6 PM
    'America/New_York', -- EST timezone
    '[1, 2, 3, 4, 5]' -- Monday through Friday (1=Monday, 7=Sunday)
) ON CONFLICT (key) DO NOTHING;

-- Insert enhanced-catalog-ui flag for time window demonstration
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, window_start_time, window_end_time, time_zone, window_days
) VALUES (
    'enhanced-catalog-ui',
    'Enhanced Catalog UI',
    'Shows enhanced catalog with additional features during business hours when support team is available',
    3, -- Time window
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "products", "type": "time-window", "component": "catalog-ui", "reason": "support-availability"}',
    '09:00:00', -- 9 AM
    '18:00:00', -- 6 PM
    'America/New_York', -- EST timezone
    '[1, 2, 3, 4, 5]' -- Monday through Friday (1=Monday, 7=Sunday)
) ON CONFLICT (key) DO NOTHING;

-- Insert new-payment-processor flag for service layer integration
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, targeting_rules, percentage_enabled
) VALUES (
    'new-payment-processor',
    'New Payment Processor Integration',
    'Progressive rollout of enhanced payment processing system with automatic fallback to legacy processor',
    4, -- User targeted with rich context
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"service": "payments", "type": "processor-migration", "component": "service-layer", "criticality": "high"}',
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
    '{"tenant": "premium", "type": "access-control", "tier": "premium"}',
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
    '{"tenant": "multi", "type": "beta-program", "phase": "phase1"}',
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
    5, -- Percentage rollout
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "rollout", "type": "percentage", "component": "dashboard"}',
    60
) ON CONFLICT (key) DO NOTHING;

-- Insert tenant-mixed-controls flag combining all tenant controls
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, disabled_tenants, tenant_percentage_enabled
) VALUES (
    'tenant-mixed-controls',
    'Advanced Multi-Tenant Controls',
    'Complex tenant control combining explicit lists and percentage rollout',
    1, -- Enabled
    'system',
    'system',
    '{"v1": "legacy", "v2": "new", "v3": "experimental"}',
    'v1',
    '{"tenant": "complex", "type": "mixed-control", "strategy": "hybrid"}',
    '["always-enabled-tenant", "vip-priority-client"]',
    '["never-enabled-tenant", "maintenance-mode-client"]',
    40 -- 40% of remaining tenants get the feature
) ON CONFLICT (key) DO NOTHING;

-- Insert tenant-region-specific flag for geographic tenant targeting
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, targeting_rules
) VALUES (
    'tenant-region-specific',
    'Regional Features for Tenants',
    'Enable region-specific features for North American tenants with fallback rules',
    4, -- User targeted
    'system',
    'system',
    '{"na": "north-america", "eu": "europe", "ap": "asia-pacific", "off": "disabled"}',
    'off',
    '{"tenant": "regional", "type": "geo-targeting", "scope": "multi-region"}',
    '["northam-tenant-1", "northam-tenant-2", "canada-corp"]',
    '[
        {
            "attribute": "region",
            "operator": 0,
            "values": ["US", "CA", "MX"],
            "variation": "na"
        },
        {
            "attribute": "region",
            "operator": 0,
            "values": ["DE", "FR", "UK"],
            "variation": "eu"
        },
        {
            "attribute": "region",
            "operator": 0,
            "values": ["JP", "SG", "AU"],
            "variation": "ap"
        }
    ]'
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

-- Insert tenant-maintenance-override flag for maintenance scenarios
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, disabled_tenants, is_permanent
) VALUES (
    'tenant-maintenance-override',
    'Tenant Maintenance Override',
    'Allow specific tenants to access system during maintenance windows',
    1, -- Enabled
    'system',
    'system',
    '{"on": true, "off": false}',
    'off',
    '{"tenant": "maintenance", "type": "override", "criticality": "high"}',
    '["critical-client-1", "emergency-services-corp", "healthcare-priority"]',
    '["non-essential-tenant", "dev-testing-org"]',
    true
) ON CONFLICT (key) DO NOTHING;

-- Insert tenant-feature-trial flag for time-limited tenant trials
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, expiration_date
) VALUES (
    'tenant-feature-trial',
    'Tenant Feature Trial Period',
    'Limited-time trial of advanced features for select tenants',
    1, -- Enabled
    'system',
    'system',
    '{"trial": "trial-version", "full": "full-version", "off": "disabled"}',
    'off',
    '{"tenant": "trial", "type": "time-limited", "duration": "30-days"}',
    '["trial-tenant-alpha", "trial-tenant-beta", "prospect-enterprise"]',
    NOW() + INTERVAL '30 days'
) ON CONFLICT (key) DO NOTHING;

-- Insert tenant-ab-test flag for tenant-level A/B testing
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, tenant_percentage_enabled
) VALUES (
    'tenant-ab-test',
    'Tenant A/B Testing Framework',
    'A/B test different pricing models across tenant segments',
    1, -- Enabled
    'system',
    'system',
    '{"pricing-v1": "legacy-pricing", "pricing-v2": "new-pricing", "pricing-v3": "dynamic-pricing"}',
    'pricing-v1',
    '{"tenant": "ab-test", "type": "pricing-experiment", "experiment": "pricing-model"}',
    '["control-group-tenant-1", "control-group-tenant-2"]',
    75 -- 75% of tenants participate in the experiment
) ON CONFLICT (key) DO NOTHING;

-- Insert tenant-security-enhanced flag for high-security tenants
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, targeting_rules
) VALUES (
    'tenant-security-enhanced',
    'Enhanced Security Features',
    'Additional security controls for high-security and government tenants',
    4, -- User targeted
    'system',
    'system',
    '{"standard": "standard-security", "enhanced": "enhanced-security", "maximum": "maximum-security"}',
    'standard',
    '{"tenant": "security", "type": "security-level", "compliance": "high"}',
    '["gov-agency-1", "finance-secure-corp", "healthcare-hipaa", "defense-contractor"]',
    '[
        {
            "attribute": "securityClearance",
            "operator": 0,
            "values": ["confidential", "secret", "top-secret"],
            "variation": "maximum"
        },
        {
            "attribute": "complianceLevel",
            "operator": 0,
            "values": ["hipaa", "sox", "pci-dss"],
            "variation": "enhanced"
        }
    ]'
) ON CONFLICT (key) DO UPDATE SET targeting_rules = EXCLUDED.targeting_rules;

-- Insert tenant-api-limits flag for tenant-specific API limitations
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, disabled_tenants, tenant_percentage_enabled
) VALUES (
    'tenant-api-limits',
    'Tenant API Rate Limits',
    'Different API rate limits based on tenant tier and subscription',
    1, -- Enabled
    'system',
    'system',
    '{"basic": {"limit": 1000, "burst": 100}, "premium": {"limit": 10000, "burst": 1000}, "enterprise": {"limit": 100000, "burst": 10000}}',
    'basic',
    '{"tenant": "api-limits", "type": "rate-limiting", "component": "api-gateway"}',
    '["premium-api-client", "enterprise-heavy-user", "integration-partner"]',
    '["rate-limited-tenant", "abuse-detected-client"]',
    100 -- All tenants get some form of rate limiting
) ON CONFLICT (key) DO NOTHING;

-- Insert tenant-data-residency flag for data location requirements
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, targeting_rules
) VALUES (
    'tenant-data-residency',
    'Data Residency Compliance',
    'Ensure tenant data is stored in compliant geographic regions',
    4, -- User targeted
    'system',
    'system',
    '{"us": "us-east", "eu": "eu-central", "ap": "ap-southeast", "ca": "ca-central"}',
    'us',
    '{"tenant": "data-residency", "type": "compliance", "regulation": "gdpr-ccpa"}',
    '["eu-gdpr-client", "ca-privacy-corp", "us-government-agency"]',
    '[
        {
            "attribute": "dataResidency",
            "operator": 0,
            "values": ["EU"],
            "variation": "eu"
        },
        {
            "attribute": "dataResidency",
            "operator": 0,
            "values": ["CA"],
            "variation": "ca"
        },
        {
            "attribute": "dataResidency",
            "operator": 0,
            "values": ["AP"],
            "variation": "ap"
        }
    ]'
) ON CONFLICT (key) DO NOTHING;

-- Insert tenant-custom-branding flag for white-label scenarios
INSERT INTO usage_demo.feature_flags (
    key, name, description, status, created_by, updated_by,
    variations, default_variation, tags, enabled_tenants, tenant_percentage_enabled, window_start_time, window_end_time, time_zone, window_days
) VALUES (
    'tenant-custom-branding',
    'Custom Branding for Tenants',
    'Enable custom branding during business hours for white-label clients',
    3, -- Time window
    'system',
    'system',
    '{"standard": "default-brand", "custom": "custom-brand", "premium": "premium-brand"}',
    'standard',
    '{"tenant": "branding", "type": "white-label", "component": "ui"}',
    '["white-label-client-1", "reseller-partner", "custom-brand-corp"]',
    25, -- Only 25% of tenants get custom branding automatically
    '08:00:00', -- 8 AM
    '20:00:00', -- 8 PM
    'America/New_York',
    '[1, 2, 3, 4, 5]' -- Weekdays only for custom branding support
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
GRANT ALL PRIVILEGES ON SCHEMA usage_demo TO propel_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA usage_demo TO propel_user;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA usage_demo TO propel_user;