using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;
using Propel.FeatureFlags.PostgresSql;
using Propel.FeatureFlags.Redis;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace FeatureFlags.IntegrationTests.Core.Client;

public class IsEnabledAsync_WithEnabledFlag(FeatureFlagClientTestFixture fixture) : IClassFixture<FeatureFlagClientTestFixture>
{
	[Fact]
	public async Task ThenReturnsTrue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagClientTestFixture.CreateTestFlag("client-enabled", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.IsEnabledAsync("client-enabled", userId: "user123");

		// Assert
		result.ShouldBeTrue();
	}
}

public class IsEnabledAsync_WithTargetedFlag(FeatureFlagClientTestFixture fixture) : IClassFixture<FeatureFlagClientTestFixture>
{
	[Fact]
	public async Task If_UserTargeted_ThenReturnsTrue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagClientTestFixture.CreateTestFlag("client-targeted", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.Equals,
				Values = ["us-west"],
				Variation = "regional-feature"
			}
		];
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.IsEnabledAsync("client-targeted", 
			userId: "user123", 
			attributes: new Dictionary<string, object> { { "region", "us-west" } });

		// Assert
		result.ShouldBeTrue();
	}
}

public class GetVariationAsync_WithStringVariation(FeatureFlagClientTestFixture fixture) : IClassFixture<FeatureFlagClientTestFixture>
{
	[Fact]
	public async Task ThenReturnsVariationValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagClientTestFixture.CreateTestFlag("client-variation", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "userId",
				Operator = TargetingOperator.Equals,
				Values = ["premium-user"],
				Variation = "premium-config"
			}
		];
		flag.Variations = new FlagVariations
		{
			Values = new Dictionary<string, object>
			{
				{ "premium-config", "premium-dashboard" }
			}
		};
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.GetVariationAsync("client-variation", "default", userId: "premium-user");

		// Assert
		result.ShouldBe("premium-dashboard");
	}
}

public class GetVariationAsync_WithDisabledFlag(FeatureFlagClientTestFixture fixture) : IClassFixture<FeatureFlagClientTestFixture>
{
	[Fact]
	public async Task ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagClientTestFixture.CreateTestFlag("client-disabled", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.GetVariationAsync("client-disabled", "fallback-value", userId: "user123");

		// Assert
		result.ShouldBe("fallback-value");
	}
}

public class EvaluateAsync_WithTimeWindowFlag(FeatureFlagClientTestFixture fixture) : IClassFixture<FeatureFlagClientTestFixture>
{
	[Fact]
	public async Task If_WithinWindow_ThenReturnsEnabledResult()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagClientTestFixture.CreateTestFlag("client-window", FlagEvaluationMode.TimeWindow);
		flag.OperationalWindow = FlagOperationalWindow.AlwaysOpen;
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.EvaluateAsync("client-window", tenantId: "tenant123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}
}

public class Client_WithTenantAndUser(FeatureFlagClientTestFixture fixture) : IClassFixture<FeatureFlagClientTestFixture>
{
	[Fact]
	public async Task If_TenantTargeted_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagClientTestFixture.CreateTestFlag("client-tenant", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "tenantId",
				Operator = TargetingOperator.Equals,
				Values = ["enterprise-tenant"],
				Variation = "enterprise-features"
			}
		];
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.IsEnabledAsync("client-tenant", 
			tenantId: "enterprise-tenant", 
			userId: "admin-user");

		// Assert
		result.ShouldBeTrue();
	}
}

public class FeatureFlagClientTestFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;

	public FeatureFlagClient Client { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;

	private ConnectionMultiplexer _redisConnection = null!;
	private readonly ILogger<PostgreSQLFeatureFlagRepository> _repositoryLogger;
	private readonly ILogger<RedisFeatureFlagCache> _cacheLogger;

	public FeatureFlagClientTestFixture()
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

		_repositoryLogger = new Mock<ILogger<PostgreSQLFeatureFlagRepository>>().Object;
		_cacheLogger = new Mock<ILogger<RedisFeatureFlagCache>>().Object;
	}

	public async Task InitializeAsync()
	{
		await _postgresContainer.StartAsync();
		await _redisContainer.StartAsync();

		var postgresConnectionString = _postgresContainer.GetConnectionString();
		await CreateFeatureFlagsTable(postgresConnectionString);
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

		var evaluator = new FeatureFlagEvaluator(Repository, evaluationManager, Cache);
		Client = new FeatureFlagClient(evaluator, "UTC");
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

	public static FeatureFlag CreateTestFlag(string key, FlagEvaluationMode evaluationMode)
	{
		var flag = new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for client integration tests",
			AuditRecord = FlagAuditRecord.NewFlag("test-user")
		};
		flag.EvaluationModeSet.AddMode(evaluationMode);
		return flag;
	}

	private static async Task CreateFeatureFlagsTable(string connectionString)
	{
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();

		var createTableSql = @"
			CREATE TABLE IF NOT EXISTS feature_flags (
				key VARCHAR(255) PRIMARY KEY,
				name VARCHAR(500) NOT NULL,
				description TEXT,
				evaluation_modes JSONB NOT NULL,
				created_at TIMESTAMP NOT NULL,
				updated_at TIMESTAMP,
				created_by VARCHAR(255) NOT NULL,
				updated_by VARCHAR(255),
				expiration_date TIMESTAMP,
				scheduled_enable_date TIMESTAMP,
				scheduled_disable_date TIMESTAMP,
				window_start_time TIME,
				window_end_time TIME,
				time_zone VARCHAR(100),
				window_days JSONB,
				percentage_enabled INTEGER NOT NULL DEFAULT 0,
				targeting_rules JSONB,
				enabled_users JSONB,
				disabled_users JSONB,
				enabled_tenants JSONB,
				disabled_tenants JSONB,
				tenant_percentage_enabled INTEGER NOT NULL DEFAULT 0,
				variations JSONB,
				default_variation VARCHAR(255),
				tags JSONB,
				is_permanent BOOLEAN NOT NULL DEFAULT false
			);";

		using var command = new NpgsqlCommand(createTableSql, connection);
		await command.ExecuteNonQueryAsync();
	}
}