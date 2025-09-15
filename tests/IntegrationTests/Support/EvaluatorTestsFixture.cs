using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.ApplicationScope;
using Propel.FeatureFlags.Evaluation.Handlers;
using Propel.FeatureFlags.Infrastructure.PostgresSql;
using Propel.FeatureFlags.Redis;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace FeatureFlags.IntegrationTests.Support;

public class EvaluatorTestsFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;

	public IApplicationFeatureFlagEvaluator Evaluator { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;

	private ConnectionMultiplexer _redisConnection = null!;
	private readonly ILogger<PostgreSQLFeatureFlagRepository> _repositoryLogger;
	private readonly ILogger<RedisFeatureFlagCache> _cacheLogger;

	public EvaluatorTestsFixture()
	{
		_postgresContainer = new PostgreSqlBuilder()
			.WithImage("postgres:15-alpine")
			.WithDatabase("featureflags_test")
			.WithUsername("test_user")
			.WithPassword("test_password")
			.WithPortBinding(5432, true)
			.Build();

		_redisContainer = new RedisBuilder()
			.WithImage("redis:7-alpine")
			.WithPortBinding(6379, true)
			.Build();

		_repositoryLogger = new Mock<ILogger<PostgreSQLFeatureFlagRepository>>().Object;
		_cacheLogger = new Mock<ILogger<RedisFeatureFlagCache>>().Object;
	}

	public async Task InitializeAsync()
	{
		await _postgresContainer.StartAsync();
		await _redisContainer.StartAsync();

		var postgresConnectionString = _postgresContainer.GetConnectionString();
		await TestHelpers.CreatePostgresTables(postgresConnectionString);
		Repository = new PostgreSQLFeatureFlagRepository(postgresConnectionString, _repositoryLogger);

		var redisConnectionString = _redisContainer.GetConnectionString();
		_redisConnection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
		Cache = new RedisFeatureFlagCache(_redisConnection, _cacheLogger);

		var evaluationManager = new FlagEvaluationManager([
			new ActivationScheduleEvaluator(),
			new OperationalWindowEvaluator(),
			new TargetingRulesEvaluator(),
			new TenantRolloutEvaluator(),
			new TerminalStateEvaluator(),
			new UserRolloutEvaluator(),
		]);

		Evaluator = new IApplicationFeatureFlagEvaluator(Repository, evaluationManager, Cache);
	}

	public async Task DisposeAsync()
	{
		_redisConnection?.Dispose();
		await _postgresContainer.DisposeAsync();
		await _redisContainer.DisposeAsync();
	}

	public async Task ClearAllData()
	{
		var connectionString = _postgresContainer.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();

		await Cache.ClearAsync();
	}
}
