using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.PostgresSql;
using Testcontainers.PostgreSql;

namespace FeatureFlags.IntegrationTests.Support;

public class PostgresRepoTestsFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _container;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	private readonly ILogger<PostgreSQLFeatureFlagRepository> _logger;

	public PostgresRepoTestsFixture()
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:15-alpine")
			.WithDatabase("feature_flags_test")
			.WithUsername("test_user")
			.WithPassword("test_password")
			.WithPortBinding(5432, true)
			.Build();

		_logger = new Mock<ILogger<PostgreSQLFeatureFlagRepository>>().Object;
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();
		var connectionString = _container.GetConnectionString();

		Repository = new PostgreSQLFeatureFlagRepository(connectionString, _logger);

		// Create the feature_flags table
		await TestHelpers.CreatePostgresTables(connectionString);
	}

	public async Task DisposeAsync()
	{
		await _container.DisposeAsync();
	}

	public async Task ClearAllFlags()
	{
		var connectionString = _container.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();
	}
}
