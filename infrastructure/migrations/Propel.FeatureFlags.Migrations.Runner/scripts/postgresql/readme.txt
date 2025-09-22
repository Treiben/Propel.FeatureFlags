-- =============================================================================
-- Usage Instructions
-- =============================================================================

MANUAL SETUP:
1. Modify variables at the top of this script
2. Run sections in order: 01, 02, 03, 04, 05
3. Verify installation: SELECT * FROM schema_migrations;

PSQL VARIABLES SETUP:
psql -h localhost -U postgres -d postgres \
  -v dbname=your_database \
  -v schema=your_schema \
  -v owner=your_user \
  -f postgres_schema.sql

MIGRATION APPROACH:
- Use schema_migrations table to track versions
- Create separate files for each version (V1_0_1__add_new_column.sql)
- Always include rollback scripts
- Test in development first
