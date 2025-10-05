using Microsoft.Extensions.DependencyInjection;

using Npgsql;
using Propel.FeatureFlags.Clients;

using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

using Testcontainers.PostgreSql;
using Testcontainers.Redis;


namespace FeatureFlags.IntegrationTests.Postgres.EvaluationTests;

public class FlagEvaluationTestsFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;

	public IServiceProvider Services { get; private set; } = null!;
	public IApplicationFlagService Evaluator => Services.GetRequiredService<IApplicationFlagService>();
	public IApplicationFlagClient Client => Services.GetRequiredService<IApplicationFlagClient>();
	public IFeatureFlagRepository FeatureFlagRepository => Services.GetRequiredService<IFeatureFlagRepository>();
	public IFeatureFlagCache Cache => Services.GetRequiredService<IFeatureFlagCache>();

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
		var sqlConnectionString = await StartPostgresContainer();
		var redisConnectionString = await StartRedisContainer();

		var services = new ServiceCollection();

		services.AddLogging();

		services.ConfigureFeatureFlags(options =>
		{
			options.Cache = new CacheOptions
			{
				EnableDistributedCache = true,
				Connection = redisConnectionString,
				CacheDurationInMinutes = TimeSpan.FromMinutes(1)
			};
			options.Database = new DatabaseOptions
			{
				Provider = DatabaseProvider.PostgreSQL,
				ConnectionString = sqlConnectionString
			};
		});

		Services = services.BuildServiceProvider();

		await Services.EnsureFeatureFlagDatabase();
	}

	public async Task DisposeAsync()
	{
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

	private async Task<string> StartPostgresContainer()
	{
		await _postgresContainer.StartAsync();

		var connectionString = _postgresContainer.GetConnectionString();
		return connectionString;
	}

	private async Task<string> StartRedisContainer()
	{
		await _redisContainer.StartAsync();
		var connectionString = _redisContainer.GetConnectionString();
		return connectionString;
	}
}

public static class ServiceCollectionExtensions
{
	public static IServiceCollection ConfigureFeatureFlags(this IServiceCollection services, Action<PropelConfiguration> configure)
	{
		var options = new PropelConfiguration();
		configure.Invoke(options);

		services.AddFeatureFlagServices(options);

		var cacheOptions = options.Cache;
		if (cacheOptions.EnableDistributedCache == true)
		{
			services.AddFeatureFlagRedisCache(cacheOptions.Connection);
		}
		else if (cacheOptions.EnableInMemoryCache == true)
		{
			services.AddFeatureFlagDefaultCache();
		}

		var dbOptions = options.Database;
		services.AddFeatureFlagDatabase(dbOptions.ConnectionString);

		return services;
	}
}
