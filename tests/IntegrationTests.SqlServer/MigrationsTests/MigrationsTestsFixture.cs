using FeatureFlags.IntegrationTests.SqlServer.Support;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.SqlServer;
using Testcontainers.MsSql;

namespace FeatureFlags.IntegrationTests.SqlServer.MigrationsTests;

public class MigrationsTestsFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _container;

	public SqlMigrationRepository MigrationRepository { get; private set; }

	public MigrationsTestsFixture()
	{
		_container = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithPassword("StrongP@ssw0rd!")
			.WithEnvironment("ACCEPT_EULA", "Y")
			.WithEnvironment("SA_PASSWORD", "StrongP@ssw0rd!")
			.WithPortBinding(1433, true)
			.Build();
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();

		var connectionBuilder = new SqlConnectionStringBuilder(_container.GetConnectionString())
		{
			InitialCatalog = "propel_migrations"
		};

		var connectionString = connectionBuilder.ConnectionString;

		var dbInitializer = new SqlDatabaseInitializer(connectionString,
			new Mock<ILogger<SqlDatabaseInitializer>>().Object);
		var initialized = await dbInitializer.InitializeAsync();
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize sql server database for feature flags");

		var migrationOptions = new SqlMigrationOptions(connectionString, "test_schema", "migrations", connectionBuilder.InitialCatalog);

		MigrationRepository = new SqlMigrationRepository(migrationOptions, new Mock<ILogger<SqlMigrationRepository>>().Object);
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
		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new SqlCommand("USE propel_migrations; DELETE FROM test_schema.migrations;", connection);
		await command.ExecuteNonQueryAsync();
	}

	public async Task SaveAsync(FlagEvaluationConfiguration flag,
		string name, string description)
	{
		await SqlServerDbHelpers.CreateFlagAsync(_container, flag, name, description);
	}
}
