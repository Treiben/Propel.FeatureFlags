namespace Propel.FeatureFlags.Migrations;

public interface IMigrationRepository
{
	string DatabaseName { get; }
	Task CreateDatabaseAsync(CancellationToken cancellationToken = default);
	Task CreateSchemaAsync(CancellationToken cancellationToken = default);
	Task CreateMigrationTableAsync(CancellationToken cancellationToken = default);
	Task<List<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default);
	Task RecordMigrationAsync(string version, string description, CancellationToken cancellationToken = default);
	Task RemoveMigrationAsync(string version, CancellationToken cancellationToken = default);
	Task<bool> ExecuteSqlAsync(string sql, CancellationToken cancellationToken = default);
}
