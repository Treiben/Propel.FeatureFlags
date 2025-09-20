# Propel Feature Flags - Database Setup and Migration Guide

This guide provides production-ready database scripts and migration strategies for both PostgreSQL and SQL Server implementations of the Propel Feature Flags system.

## Overview

The database setup supports two approaches:
1. **Manual Setup** - Run complete scripts for initial setup
2. **Migration-Based** - Incremental, versioned updates for production environments

## File Structure

```
database/
├── postgresql/
│   ├── manual-setup.sql          # Complete PostgreSQL setup
│   ├── migrations/
│   │   ├── V1_0_0__initial_schema.sql
│   │   └── V1_0_1__add_audit_fields.sql
│   └── rollback/
│       └── V1_0_0__rollback.sql
├── sqlserver/
│   ├── manual-setup.sql          # Complete SQL Server setup
│   ├── migrations/
│   │   ├── V1_0_0__initial_schema.sql
│   │   └── V1_0_1__add_audit_fields.sql
│   └── rollback/
│       └── V1_0_0__rollback.sql
└── README.md                     # This file
```

## Quick Start

### PostgreSQL Setup

#### Option 1: Manual Setup (Development/New Environments)

1. **Modify variables** in the script header:
```sql
\set dbname 'propel_feature_flags'
\set schema 'public'  
\set owner 'propel_user'
```

2. **Run the complete setup**:
```bash
# Using psql with variables
psql -h localhost -U postgres -d postgres \
  -v dbname=your_database \
  -v schema=your_schema \
  -v owner=your_user \
  -f postgresql/manual-setup.sql

# Or modify the script directly and run
psql -h localhost -U postgres -d postgres -f postgresql/manual-setup.sql
```

3. **Verify installation**:
```sql
SELECT * FROM schema_migrations;
```

#### Option 2: Migration-Based Setup (Production)

1. **Use a migration tool** like Flyway, Liquibase, or dbmate:
```bash
# Example with Flyway
flyway -url=jdbc:postgresql://localhost:5432/propel \
       -user=postgres \
       migrate
```

2. **Or run migrations manually**:
```bash
psql -f migrations/V1_0_0__initial_schema.sql
psql -f migrations/V1_0_1__add_audit_fields.sql
```

### SQL Server Setup

#### Option 1: Manual Setup (Development/New Environments)

1. **Modify variables** in the script header:
```sql
DECLARE @DatabaseName NVARCHAR(128) = N'PropelFeatureFlags'
DECLARE @SchemaName NVARCHAR(128) = N'dbo'
DECLARE @UserName NVARCHAR(128) = N'propel_user'
```

2. **Run the complete setup**:
```bash
# Using sqlcmd
sqlcmd -S localhost -d master -E -i sqlserver/manual-setup.sql

# Or using SQL Server Management Studio
# Open and execute the script
```

3. **Verify installation**:
```sql
SELECT * FROM schema_migrations;
```

#### Option 2: Migration-Based Setup (Production)

1. **Use SQL Server migration tools** or custom scripts:
```bash
# Example with custom migration runner
sqlcmd -S server -d database -E -i migrations/V1_0_0__initial_schema.sql
```

## Schema Overview

### Core Tables

| Table | Purpose |
|-------|---------|
| `feature_flags` | Main feature flag configurations and rules |
| `feature_flags_metadata` | Additional metadata, tags, and retention policies |
| `feature_flags_audit` | Audit trail of all flag changes |
| `schema_migrations` | Version tracking for database migrations |

### Key Features

- **Composite Primary Key**: `(key, application_name, application_version, scope)`
- **JSON Storage**: Evaluation modes, targeting rules, user lists (JSONB in PostgreSQL, NVARCHAR(MAX) in SQL Server)
- **Flexible Targeting**: Support for users, tenants, percentages, time windows, and custom rules
- **Audit Trail**: Complete change history with actor, timestamp, and reason
- **Performance Indexes**: Optimized for common query patterns
- **Automatic Timestamps**: Created/updated timestamps with triggers

## Migration Strategy

### Version Naming Convention

Use semantic versioning with underscores:
- `V1_0_0__initial_schema.sql` - Major version with breaking changes
- `V1_0_1__add_audit_fields.sql` - Minor version with additions
- `V1_0_1_1__fix_constraint.sql` - Patch version for fixes

### Migration Best Practices

1. **Always Test First**: Run migrations in development and staging
2. **Backup Before Migration**: Create database backups before production changes
3. **Incremental Changes**: Keep migrations small and focused
4. **Rollback Scripts**: Create rollback procedures for each migration
5. **Performance Testing**: Test with realistic data volumes
6. **Zero-Downtime**: Design migrations to avoid application downtime

### Sample Migration Structure

```sql
-- V1_0_1__add_performance_indexes.sql
-- Description: Add indexes for improved query performance
-- Author: DevOps Team
-- Date: 2024-XX-XX

-- Record migration start
INSERT INTO schema_migrations (version, description) 
VALUES ('1.0.1', 'Add performance indexes for feature flag queries');

-- Migration logic
CREATE INDEX IF NOT EXISTS ix_feature_flags_created_at 
    ON feature_flags (created_at);

-- Verify migration
SELECT COUNT(*) FROM pg_indexes 
WHERE indexname = 'ix_feature_flags_created_at';

-- Migration complete
UPDATE schema_migrations 
SET applied_at = NOW() 
WHERE version = '1.0.1';
```

## Connection Strings

### PostgreSQL
```
Host=localhost;Port=5432;Database=propel_feature_flags;Username=propel_user;Password=your_password;Search Path=public;Include Error Detail=true
```

### SQL Server
```
Server=localhost;Database=PropelFeatureFlags;User Id=propel_user;Password=your_password;TrustServerCertificate=true;
```

## Performance Considerations

### Index Strategy

The scripts create comprehensive indexes for:
- **Query Performance**: Application name, version, scope combinations
- **JSON Queries**: GIN indexes (PostgreSQL) for JSONB columns
- **Time-based Queries**: Scheduled dates and timestamps
- **Audit Queries**: Flag key, actor, and timestamp indexes

### Monitoring Queries

#### PostgreSQL
```sql
-- Check index usage
SELECT schemaname, tablename, indexname, idx_scan, idx_tup_read 
FROM pg_stat_user_indexes 
WHERE schemaname = 'public' 
ORDER BY idx_scan DESC;

-- Check table sizes
SELECT schemaname, tablename, 
       pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
FROM pg_tables 
WHERE schemaname = 'public';
```

#### SQL Server
```sql
-- Check index usage
SELECT o.name AS TableName, i.name AS IndexName, 
       s.user_seeks, s.user_scans, s.user_lookups
FROM sys.dm_db_index_usage_stats s
INNER JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
INNER JOIN sys.objects o ON i.object_id = o.object_id
WHERE o.type = 'U'
ORDER BY s.user_seeks + s.user_scans + s.user_lookups DESC;

-- Check table sizes
SELECT t.NAME AS TableName,
       p.rows AS RowCounts,
       CAST(ROUND(((SUM(a.total_pages) * 8) / 1024.00), 2) AS NUMERIC(36, 2)) AS TotalSpaceMB
FROM sys.tables t
INNER JOIN sys.indexes i ON t.OBJECT_ID = i.object_id
INNER JOIN sys.partitions p ON i.object_id = p.OBJECT_ID AND i.index_id = p.index_id
INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
GROUP BY t.Name, p.Rows
ORDER BY TotalSpaceMB DESC;
```

## Security Setup

### PostgreSQL Security
```sql
-- Create dedicated user
CREATE USER propel_user WITH PASSWORD 'secure_password';

-- Grant minimal required permissions
GRANT USAGE ON SCHEMA public TO propel_user;
GRANT SELECT, INSERT, UPDATE ON feature_flags TO propel_user;
GRANT SELECT, INSERT, UPDATE ON feature_flags_metadata TO propel_user;
GRANT SELECT, INSERT ON feature_flags_audit TO propel_user;
```

### SQL Server Security
```sql
-- Create login and user
CREATE LOGIN [propel_user] WITH PASSWORD = 'secure_password';
USE [PropelFeatureFlags];
CREATE USER [propel_user] FOR LOGIN [propel_user];

-- Grant minimal required permissions
GRANT SELECT, INSERT, UPDATE ON feature_flags TO [propel_user];
GRANT SELECT, INSERT, UPDATE ON feature_flags_metadata TO [propel_user];
GRANT SELECT, INSERT ON feature_flags_audit TO [propel_user];
```

## Troubleshooting

### Common Issues

1. **Permission Denied**: Ensure the user has appropriate database creation privileges
2. **Connection Timeout**: Check network connectivity and connection string parameters
3. **JSON Validation Errors**: Verify JSON format in data before insertion
4. **Index Creation Failures**: Check for existing indexes or insufficient permissions

### Rollback Procedures

If migration fails:

1. **Stop application** to prevent data corruption
2. **Restore from backup** if available
3. **Run rollback scripts** in reverse order
4. **Verify data integrity** before resuming operations

### Monitoring and Maintenance

- **Regular Backups**: Implement automated backup schedules
- **Index Maintenance**: Monitor and rebuild fragmented indexes
- **Audit Cleanup**: Archive old audit records based on retention policies
- **Performance Monitoring**: Track query performance and optimize as needed

## Support and Documentation

- **Schema Changes**: Document all schema modifications in migration files
- **Performance Issues**: Monitor slow queries and index effectiveness
- **Security Reviews**: Regular audits of user permissions and access patterns
- **Backup Verification**: Test restore procedures regularly

For additional support, consult the main Propel Feature Flags documentation or contact the development team.