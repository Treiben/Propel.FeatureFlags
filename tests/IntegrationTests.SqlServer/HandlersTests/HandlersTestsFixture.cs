using Microsoft.Data.SqlClient;
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
using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace IntegrationTests.SqlServer.HandlersTests;

public class HandlersTestsFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _sqlContainer;
	private readonly RedisContainer _redisContainer;

	public IServiceProvider Services {get; private set; } = null!;
	public IDashboardRepository DashboardRepository => Services.GetRequiredService<IDashboardRepository>();

	public IFeatureFlagCache Cache => Services.GetRequiredService<IFeatureFlagCache>();

	public HandlersTestsFixture()
	{
		_sqlContainer = new MsSqlBuilder()
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
		var sqlConnectionString = await StartSqlContainer();
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
				Provider = DatabaseProvider.SqlServer,
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
		await _sqlContainer.DisposeAsync();
		await _redisContainer.DisposeAsync();
	}

	public async Task ClearAllData()
	{
		var connectionString = _sqlContainer.GetConnectionString();
		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new SqlCommand("DELETE FROM FeatureFlags", connection);
		await command.ExecuteNonQueryAsync();

		await Cache.ClearAsync();
	}

	private async Task<string> StartSqlContainer()
	{
		await _sqlContainer.StartAsync();

		var connectionString = _sqlContainer.GetConnectionString();
		return connectionString;
	}

	private async Task<string> StartRedisContainer()
	{
		await _redisContainer.StartAsync();
		var connectionString = _redisContainer.GetConnectionString();
		return connectionString;
	}
}
