using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.PostgresSql;
using Propel.FeatureFlags.Redis;
using Propel.FlagsManagement.Api;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Shared;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace FlagsManagementApi.IntegrationTests.Support;

public class FlagsManagementApiFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	private ServiceProvider _serviceProvider = null!;
	private ConnectionMultiplexer _redisConnection = null!;

	public CreateFlagHandler CreateFlagHandler { get; private set; } = null!;
	public DeleteFlagHandler DeleteFlagHandler { get; private set; } = null!;
	public FlagEvaluationHandler FlagEvaluationHandler { get; private set; } = null!;
	public ManageTenantAccessHandler ManageTenantAccessHandler { get; private set; } = null!;
	public ManageUserAccessHandler ManageUserAccessHandler { get; private set; } = null!;
	public ToggleFlagHandler ToggleFlagHandler { get; private set; } = null!;
	public UpdateFlagHandler UpdateFlagHandler { get; private set; } = null!;
	public UpdateScheduleHandler UpdateScheduleHandler { get; private set; } = null!;
	public UpdateTenantRolloutPercentageHandler UpdateTenantRolloutPercentageHandler { get; private set; } = null!;
	public UpdateTimeWindowHandler UpdateTimeWindowHandler { get; private set; } = null!;
	public UpdateUserRolloutPercentageHandler UpdateUserRolloutPercentageHandler { get; private set; } = null!;


	public IFeatureFlagRepository Repository { get; private set; } = null!;

	public IFeatureFlagCache? Cache { get; private set; }
	public Mock<ICurrentUserService> MockCurrentUserService { get; private set; } = null!;

	public FlagsManagementApiFixture()
	{
		_postgresContainer = new PostgreSqlBuilder()
			.WithImage("postgres:15-alpine")
			.WithDatabase("flagsmanagement_api_test")
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
		// Start containers
		await _postgresContainer.StartAsync();
		await _redisContainer.StartAsync();

		// Setup service collection
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddConsole());

		// Setup mocks
		MockCurrentUserService = new Mock<ICurrentUserService>();
		MockCurrentUserService.Setup(x => x.UserName).Returns("test-user");
		MockCurrentUserService.Setup(x => x.UserId).Returns("test-user-id");
		services.AddSingleton(MockCurrentUserService.Object);

		// Setup PostgreSQL
		var postgresConnectionString = _postgresContainer.GetConnectionString();
		services.AddPostgresSqlFeatureFlags(postgresConnectionString);
		// Setup Redis cache
		var redisConnectionString = _redisContainer.GetConnectionString();
		services.AddRedisCache(_redisContainer.GetConnectionString());

		// Register feature flag services
		services.AddFeatureFlags(new Propel.FeatureFlags.Core.FeatureFlagConfigurationOptions
		{
			UseCache = true
		});

		// Register api handlers
		services.AddHandlers();

		// Build service provider
		_serviceProvider = services.BuildServiceProvider();

		// Initialize database
		await _serviceProvider.EnsureFeatureFlagsDatabaseAsync();

		// Get infrastructure services
		Repository = _serviceProvider.GetRequiredService<IFeatureFlagRepository>();
		Cache = _serviceProvider.GetService<IFeatureFlagCache>();

		// Get services
		CreateFlagHandler = _serviceProvider.GetRequiredService<CreateFlagHandler>();
		DeleteFlagHandler = _serviceProvider.GetRequiredService<DeleteFlagHandler>();
		FlagEvaluationHandler = _serviceProvider.GetRequiredService<FlagEvaluationHandler>();
		ManageTenantAccessHandler = _serviceProvider.GetRequiredService<ManageTenantAccessHandler>();
		ManageUserAccessHandler = _serviceProvider.GetRequiredService<ManageUserAccessHandler>();
		ToggleFlagHandler = _serviceProvider.GetRequiredService<ToggleFlagHandler>();
		UpdateFlagHandler = _serviceProvider.GetRequiredService<UpdateFlagHandler>();
		UpdateScheduleHandler = _serviceProvider.GetRequiredService<UpdateScheduleHandler>();
		UpdateTenantRolloutPercentageHandler = _serviceProvider.GetRequiredService<UpdateTenantRolloutPercentageHandler>();
		UpdateTimeWindowHandler = _serviceProvider.GetRequiredService<UpdateTimeWindowHandler>();
		UpdateUserRolloutPercentageHandler = _serviceProvider.GetRequiredService<UpdateUserRolloutPercentageHandler>();
	}

	public async Task DisposeAsync()
	{
		_redisConnection?.Dispose();
		_serviceProvider?.Dispose();
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
	}
}