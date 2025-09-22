using FeatureFlags.IntegrationTests.PostgreTests;
using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Infrastructure.PostgresSql;
using Propel.FeatureFlags.Infrastructure.Redis;
using Propel.FeatureFlags.Services;
using Propel.FeatureFlags.Services.ApplicationScope;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace FeatureFlags.IntegrationTests.EvaluationTests;

public class FlagEvaluationTestsFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;

	public IFeatureFlagEvaluator Evaluator { get; private set; } = null!;
	public IFeatureFlagClient Client { get; private set; } = null!;
	public ClientApplicationRepository EvaluationRepository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;

	private ConnectionMultiplexer _redisConnection = null!;

	public FlagEvaluationTestsFixture()
	{
		_postgresContainer = new PostgreSqlBuilder()
			.WithImage("postgres:15-alpine")
			.WithDatabase("featureflags_client")
			.WithUsername("test_user")
			.WithPassword("test_password")
			.WithPortBinding(5432, true)
			.Build();

		_redisContainer = new RedisBuilder()
			.WithImage("redis:7-alpine")
			.WithPortBinding(6379, true)
			.Build();
	}

	public async Task InitializeAsync()
	{
		await _postgresContainer.StartAsync();
		await _redisContainer.StartAsync();

		var connectionString = _postgresContainer.GetConnectionString();

		var dbInitializer = new PostgreSQLDatabaseInitializer(connectionString, new Mock<ILogger<PostgreSQLDatabaseInitializer>>().Object);
		var initialized = await dbInitializer.InitializeAsync();
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize PostgreSQL database for feature flags");

		EvaluationRepository = new ClientApplicationRepository(connectionString, new Mock<ILogger<ClientApplicationRepository>>().Object);

		var redisConnectionString = _redisContainer.GetConnectionString();
		_redisConnection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);

		var options = new PropelOptions
		{
			Cache = new CacheOptions
			{
				CacheDurationInMinutes = TimeSpan.FromMinutes(1)
			}
		};

		Cache = new RedisFeatureFlagCache(_redisConnection, options, new Mock<ILogger<RedisFeatureFlagCache>>().Object);

		var evaluationManager = new FlagEvaluationManager([
			new ActivationScheduleEvaluator(),
			new OperationalWindowEvaluator(),
			new TargetingRulesEvaluator(),
			new TenantRolloutEvaluator(),
			new TerminalStateEvaluator(),
			new UserRolloutEvaluator(),
		]);

		Evaluator = new FeatureFlagEvaluator(EvaluationRepository, evaluationManager, Cache);
		Client = new FeatureFlagClient(Evaluator, "UTC");
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

	public async Task SaveAsync(FlagEvaluationConfiguration flag,
		string name, string description)
	{
		await PosgreDbHelpers.CreateFlagAsync(_postgresContainer, flag, name, description);
	}
}
