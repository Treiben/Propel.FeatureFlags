using FeatureFlags.IntegrationTests.Postgres.Support;

using Microsoft.Extensions.DependencyInjection;
using Npgsql;

using Propel.FeatureFlags.Dashboard.Api;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Extensions;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Infrastructure.Extensions;

using System.Text.Json;
using System.Text.Json.Serialization;

using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace IntegrationTests.Postgres.HandlersTests;

public class HandlersTestsFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;

	public IServiceProvider Services {get; private set; } = null!;
	public IDashboardRepository DashboardRepository => Services.GetRequiredService<IDashboardRepository>();

	public IFeatureFlagCache Cache => Services.GetRequiredService<IFeatureFlagCache>();

	public HandlersTestsFixture()
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

		services.ConfigureHttpJsonOptions(options =>
		{
			options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
			options.SerializerOptions.WriteIndented = true;
			options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
			options.SerializerOptions.Converters.Add(new EnumJsonConverter<EvaluationMode>());
			options.SerializerOptions.Converters.Add(new EnumJsonConverter<DayOfWeek>());
			options.SerializerOptions.Converters.Add(new EnumJsonConverter<TargetingOperator>());
		});

		var mockCurrentUserService = new Mock<ICurrentUserService>();
		mockCurrentUserService.Setup(s => s.UserName).Returns("integration-test-user");
		mockCurrentUserService.Setup(s => s.UserId).Returns("integration-test-user-id");

		services.AddSingleton<ICurrentUserService>(mockCurrentUserService.Object);

		// Configure dashboard-specific services
		var options = new PropelOptions
		{
			Cache = new CacheOptions
			{
				EnableInMemoryCache = false,
				EnableDistributedCache = false,
				Connection = _redisContainer.GetConnectionString()
			},
			Database = new DatabaseOptions
			{
				Provider = DatabaseProvider.PostgreSQL,
				ConnectionString = sqlConnectionString
			}
		};
		services.AddSingleton(options);

		services.AddFeatureFlagRedisCache(options.Cache.Connection);
		services.AddDatabase(options);

		services.AddDatabaseInitializer(sqlConnectionString);

		services.RegisterEvaluators();
		services.AddDashboardServices();

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

	public async Task SaveAsync(FlagEvaluationConfiguration flag,
		string name, string description)
	{
		await PostgresDbHelpers.CreateFlagAsync(_postgresContainer, flag, name, description);
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
