using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;
using Propel.FlagsManagement.Api;
using Propel.FlagsManagement.Api.Endpoints.Shared;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace FlagsManagementApi.IntegrationTests.Support;

public class FlagsManagementApiFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;

	private readonly ConnectionMultiplexer _redisConnection = null!;

	private ServiceProvider _serviceProvider = null!;

	public IFlagManagementRepository ManagementRepository { get; private set; } = null!;

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

		var options = new FeatureFlagConfigurationOptions
		{
			SqlConnectionString = _postgresContainer.GetConnectionString(),
			RedisConnectionString = _redisContainer.GetConnectionString(),
			CacheOptions = new CacheOptions
			{
				UseCache = true,
				ExpiryInMinutes = TimeSpan.FromMinutes(2)
			}
		};
		// Configure flags management api services
		services
			.RegisterPropelFeatureFlagServices(options)
			.RegisterPropelManagementApServicesi()
			.AddPropelHealthchecks(options.SqlConnectionString!, options.RedisConnectionString!);

		// Setup mocks
		MockCurrentUserService = new Mock<ICurrentUserService>();
		MockCurrentUserService.Setup(x => x.UserName).Returns("test-user");
		MockCurrentUserService.Setup(x => x.UserId).Returns("test-user-id");
		services.AddSingleton(MockCurrentUserService.Object);

		// Build service provider
		_serviceProvider = services.BuildServiceProvider();

		// Initialize database
		await _serviceProvider.EnsureFeatureFlagsDatabaseAsync();

		// Get infrastructure services
		ManagementRepository = _serviceProvider.GetRequiredService<IFlagManagementRepository>();
		Cache = _serviceProvider.GetService<IFeatureFlagCache>();
	}

	public T GetHandler<T>() where T : class
	{
		return _serviceProvider.GetRequiredService<T>();
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