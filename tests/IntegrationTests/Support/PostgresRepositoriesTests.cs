using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Infrastructure.PostgresSql;
using Testcontainers.PostgreSql;

namespace FeatureFlags.IntegrationTests.Support;

public class PostgresRepositoriesTests : IAsyncLifetime
{
	private readonly PostgreSqlContainer _container;
	public FlagEvaluationRepository EvaluationRepository { get; private set; } = null!;
	public FlagManagementRepository ManagementRepository { get; private set; } = null!;

	public PostgresRepositoriesTests()
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

		var connectionString = _container.GetConnectionString();

		var dbInitializer = new PostgreSQLDatabaseInitializer(connectionString, new Mock<ILogger<PostgreSQLDatabaseInitializer>>().Object);
		var initialized = await dbInitializer.InitializeAsync();
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize PostgreSQL database for feature flags");

		EvaluationRepository = new FlagEvaluationRepository(connectionString, new Mock<ILogger<FlagEvaluationRepository>>().Object);
		ManagementRepository = new FlagManagementRepository(connectionString, new Mock<ILogger<FlagManagementRepository>>().Object);
	}

	public async Task DisposeAsync()
	{
		await _container.DisposeAsync();
	}

	public async Task ClearAllData()
	{
		var connectionString = _container.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();
	}
}
