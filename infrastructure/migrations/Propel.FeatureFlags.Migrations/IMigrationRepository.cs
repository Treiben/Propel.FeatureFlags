namespace Propel.FeatureFlags.Migrations;

public interface IMigrationRepository
{
	string Database { get; }
	Task CreateDatabaseAsync(CancellationToken cancellationToken = default);
	Task CreateSchemaAsync(CancellationToken cancellationToken = default);
	Task CreateMigrationTableAsync(CancellationToken cancellationToken = default);
	Task<List<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default);
	Task RecordMigrationAsync(string version, string description, CancellationToken cancellationToken = default);
	Task RemoveMigrationAsync(string version, CancellationToken cancellationToken = default);
	Task<bool> ExecuteSqlAsync(string sql, CancellationToken cancellationToken = default);
}

public class DuplicatedVersionException : Exception
{
	public DuplicatedVersionException(string version)
		: base($"A migration with version '{version}' already exists.")
	{
	}
}
