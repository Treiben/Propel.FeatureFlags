using FeatureFlags.IntegrationTests.SqlServer.Support;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Extensions;
using Propel.FeatureFlags.FlagEvaluationServices.ApplicationScope;

using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Infrastructure.Extensions;
using Testcontainers.MsSql;
using Testcontainers.Redis;


namespace FeatureFlags.IntegrationTests.SqlServer.EvaluationTests;

public class FlagEvaluationTestsFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _container;
	private readonly RedisContainer _redisContainer;

	public IServiceProvider Services { get; private set; } = null!;
	public IFeatureFlagEvaluator Evaluator => Services.GetRequiredService<IFeatureFlagEvaluator>();
	public IFeatureFlagClient Client => Services.GetRequiredService<IFeatureFlagClient>();
	public IFeatureFlagRepository FeatureFlagRepository => Services.GetRequiredService<IFeatureFlagRepository>();
	public IFeatureFlagCache Cache => Services.GetRequiredService<IFeatureFlagCache>();

	public FlagEvaluationTestsFixture()
	{
		_container = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithPassword("StrongP@ssw0rd!")
			.WithEnvironment("ACCEPT_EULA", "Y")
			.WithEnvironment("SA_PASSWORD", "StrongP@ssw0rd!")
			.WithPortBinding(1433, true)
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
		await _container.DisposeAsync();
		await _redisContainer.DisposeAsync();
	}

	public async Task ClearAllData()
	{
		var connectionString = _container.GetConnectionString();
		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new SqlCommand("DELETE FROM FeatureFlags", connection);
		await command.ExecuteNonQueryAsync();

		await Cache.ClearAsync();
	}

	public async Task SaveAsync(FlagEvaluationConfiguration flag,
		string name, string description)
	{
		await SqlServerDbHelpers.CreateFlagAsync(_container, flag, name, description);
	}

	private async Task<string> StartPostgresContainer()
	{
		await _container.StartAsync();

		var connectionString = _container.GetConnectionString();
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
	public static IServiceCollection ConfigureFeatureFlags(this IServiceCollection services, Action<PropelOptions> configure)
	{
		var options = new PropelOptions();
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
		services.AddFeatureFlagPersistence(dbOptions.ConnectionString);

		return services;
	}
}
