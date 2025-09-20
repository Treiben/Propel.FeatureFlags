namespace Propel.FeatureFlags.Migrations;

public interface IMigrationRepository
{
	Task<bool> DatabaseExistsAsync(CancellationToken cancellationToken = default);
	Task CreateDatabaseAsync(CancellationToken cancellationToken = default);
	Task<bool> MigrationTableExistsAsync(CancellationToken cancellationToken = default);
	Task CreateMigrationTableAsync(CancellationToken cancellationToken = default);
	Task<List<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default);
	Task RecordMigrationAsync(string version, string description, CancellationToken cancellationToken = default);
	Task RemoveMigrationAsync(string version, CancellationToken cancellationToken = default);
	Task<bool> ExecuteSqlAsync(string sql, CancellationToken cancellationToken = default);
}
