using FeatureFlags.IntegrationTests.Postgres.Support;

using Microsoft.Extensions.Logging;
using Npgsql;

using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.PostgresSql;
using Propel.FeatureFlags.Infrastructure.SqlServer;
using Propel.FeatureFlags.Migrations;

using Testcontainers.PostgreSql;

namespace FeatureFlags.IntegrationTests.Postgres.MigrationsTests;

public class MigrationsTestsFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _container;

	public IMigrationRepository MigrationRepository { get; private set; }

	public MigrationsTestsFixture()
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:15-alpine")
			.WithDatabase("feature_flags_test")
			.WithUsername("test_user")
			.WithPassword("test_password")
			.WithPortBinding(5432, true)
			.Build();
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();

		var connectionBuilder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
		{
			Database = "propel_migrations"
		};

		var connectionString = connectionBuilder.ConnectionString;

		var dbInitializer = new PostgresDatabaseInitializer(connectionString, new Mock<ILogger<PostgresDatabaseInitializer>>().Object);
		var initialized = await dbInitializer.InitializeAsync();
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize sql server database for feature flags");

		var migrationOptions = new SqlMigrationOptions(connectionString, "test_schema", "migrations", connectionBuilder.Database);

		MigrationRepository = new PostgreMigrationRepository(migrationOptions, new Mock<ILogger<PostgreMigrationRepository>>().Object);
	}

	public async Task DisposeAsync()
	{
		await _container.DisposeAsync();
	}

	public async Task SetupMigrations()
	{
		await MigrationRepository.CreateDatabaseAsync();
		await MigrationRepository.CreateSchemaAsync();
		await MigrationRepository.CreateMigrationTableAsync();
	}

	public async Task ClearMigrations()
	{
		var connectionString = _container.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand("DELETE FROM test_schema.propel_migrations;", connection);
		await command.ExecuteNonQueryAsync();
	}

	public async Task SaveAsync(FlagEvaluationConfiguration flag,
		string name, string description)
	{
		await PostgresDbHelpers.CreateFlagAsync(_container, flag, name, description);
	}
}
