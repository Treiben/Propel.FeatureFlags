namespace Propel.FeatureFlags.MigrationsCLI.Providers;

public interface IDatabaseProvider
{
    Task<bool> DatabaseExistsAsync(string connectionString);
    Task CreateDatabaseAsync(string connectionString);
    Task<bool> MigrationTableExistsAsync(string connectionString);
    Task CreateMigrationTableAsync(string connectionString);
    Task<IEnumerable<AppliedMigration>> GetAppliedMigrationsAsync(string connectionString);
    Task AddMigrationRecordAsync(string connectionString, string version, string description);
    Task RemoveMigrationRecordAsync(string connectionString, string version);
    Task ExecuteSqlAsync(string connectionString, string sql);
    Task<T> ExecuteScalarAsync<T>(string connectionString, string sql);
}