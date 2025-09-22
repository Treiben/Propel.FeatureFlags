namespace Propel.FeatureFlags.Infrastructure.PostgresSql;

public record PostgreSqlMigrationOptions(string Connection, string Schema = "public", string MigrationTable = "flags_schema_migrations", string Database = "propel_feature_flags");
