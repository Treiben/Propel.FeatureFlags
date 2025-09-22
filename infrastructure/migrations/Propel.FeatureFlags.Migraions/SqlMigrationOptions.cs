namespace Propel.FeatureFlags.Infrastructure.SqlServer;

public record SqlMigrationOptions(string Connection, string Schema = "dbo", string MigrationTable = "flags_schema_migrations", string Database = "propel_feature_flags");
