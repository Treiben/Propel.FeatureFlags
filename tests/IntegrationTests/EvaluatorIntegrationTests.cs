using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;
using Propel.FeatureFlags.PostgresSql;
using Propel.FeatureFlags.Redis;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace FeatureFlags.IntegrationTests.Core.Evaluator;

public class Evaluate_WithEnabledFlag : IClassFixture<FeatureFlagEvaluatorTestFixture>
{
	private readonly FeatureFlagEvaluatorTestFixture _fixture;

	public Evaluate_WithEnabledFlag(FeatureFlagEvaluatorTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsEnabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("enabled-test", FlagEvaluationMode.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _fixture.Evaluator.Evaluate("enabled-test", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Feature flag 'enabled-test' is explicitly enabled");
	}

	[Fact]
	public async Task If_FlagInCache_ThenUsesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("cached-flag", FlagEvaluationMode.Enabled);
		await _fixture.Cache.SetAsync("cached-flag", flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _fixture.Evaluator.Evaluate("cached-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}
}

public class Evaluate_WithDisabledFlag : IClassFixture<FeatureFlagEvaluatorTestFixture>
{
	private readonly FeatureFlagEvaluatorTestFixture _fixture;

	public Evaluate_WithDisabledFlag(FeatureFlagEvaluatorTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsDisabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("disabled-test", FlagEvaluationMode.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _fixture.Evaluator.Evaluate("disabled-test", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Feature flag 'disabled-test' is explicitly disabled");
	}
}

public class Evaluate_WithUserTargetedFlag : IClassFixture<FeatureFlagEvaluatorTestFixture>
{
	private readonly FeatureFlagEvaluatorTestFixture _fixture;

	public Evaluate_WithUserTargetedFlag(FeatureFlagEvaluatorTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_UserIsTargeted_ThenReturnsEnabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("targeted-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "userId",
				Operator = TargetingOperator.Equals,
				Values = ["target-user"],
				Variation = "premium-features"
			}
		];
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "target-user");

		// Act
		var result = await _fixture.Evaluator.Evaluate("targeted-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("premium-features");
		result.Reason.ShouldBe("Targeting rule matched: userId Equals target-user");
	}

	[Fact]
	public async Task If_UserNotTargeted_ThenReturnsDisabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("targeted-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "userId",
				Operator = TargetingOperator.Equals,
				Values = ["other-user"],
				Variation = "premium-features"
			}
		];
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "current-user");

		// Act
		var result = await _fixture.Evaluator.Evaluate("targeted-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class Evaluate_WithUserRolloutFlag : IClassFixture<FeatureFlagEvaluatorTestFixture>
{
	private readonly FeatureFlagEvaluatorTestFixture _fixture;

	public Evaluate_WithUserRolloutFlag(FeatureFlagEvaluatorTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_UserInAllowedList_ThenReturnsEnabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("rollout-flag", FlagEvaluationMode.UserRolloutPercentage);
		flag.UserAccess = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["allowed-user"],
			rolloutPercentage: 0);
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "allowed-user");

		// Act
		var result = await _fixture.Evaluator.Evaluate("rollout-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly allowed");
	}

	[Fact]
	public async Task If_UserInBlockedList_ThenReturnsDisabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("rollout-flag", FlagEvaluationMode.UserRolloutPercentage);
		flag.UserAccess = FlagUserAccessControl.CreateAccessControl(
			blockedUsers: ["blocked-user"],
			rolloutPercentage: 100);
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "blocked-user");

		// Act
		var result = await _fixture.Evaluator.Evaluate("rollout-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("User explicitly blocked");
	}
}

public class Evaluate_WithTimeWindowFlag : IClassFixture<FeatureFlagEvaluatorTestFixture>
{
	private readonly FeatureFlagEvaluatorTestFixture _fixture;

	public Evaluate_WithTimeWindowFlag(FeatureFlagEvaluatorTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_WithinTimeWindow_ThenReturnsEnabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("window-flag", FlagEvaluationMode.TimeWindow);
		flag.OperationalWindow = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));
		await _fixture.Repository.CreateAsync(flag);

		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _fixture.Evaluator.Evaluate("window-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_OutsideTimeWindow_ThenReturnsDisabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("window-flag", FlagEvaluationMode.TimeWindow);
		flag.OperationalWindow = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));
		await _fixture.Repository.CreateAsync(flag);

		var evaluationTime = new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await _fixture.Evaluator.Evaluate("window-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Outside time window");
	}
}

public class GetVariation_WithComplexVariations : IClassFixture<FeatureFlagEvaluatorTestFixture>
{
	private readonly FeatureFlagEvaluatorTestFixture _fixture;

	public GetVariation_WithComplexVariations(FeatureFlagEvaluatorTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_VariationIsString_ThenReturnsString()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("string-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "userId",
				Operator = TargetingOperator.Equals,
				Values = ["test-user"],
				Variation = "string-variant"
			}
		];
		flag.Variations = new FlagVariations
		{
			Values = new Dictionary<string, object>
			{
				{ "string-variant", "Hello World" }
			}
		};
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await _fixture.Evaluator.GetVariation("string-flag", "default", context);

		// Assert
		result.ShouldBe("Hello World");
	}

	[Fact]
	public async Task If_VariationIsInteger_ThenReturnsInteger()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("int-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "userId",
				Operator = TargetingOperator.Equals,
				Values = ["test-user"],
				Variation = "int-variant"
			}
		];
		flag.Variations = new FlagVariations
		{
			Values = new Dictionary<string, object>
			{
				{ "int-variant", 42 }
			}
		};
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await _fixture.Evaluator.GetVariation("int-flag", 0, context);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task If_FlagDisabled_ThenReturnsDefaultValue()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("disabled-variation", FlagEvaluationMode.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await _fixture.Evaluator.GetVariation("disabled-variation", "default-value", context);

		// Assert
		result.ShouldBe("default-value");
	}
}

public class FeatureFlagEvaluatorTestFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;

	public FeatureFlagEvaluator Evaluator { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;

	private ConnectionMultiplexer _redisConnection = null!;
	private readonly ILogger<PostgreSQLFeatureFlagRepository> _repositoryLogger;
	private readonly ILogger<RedisFeatureFlagCache> _cacheLogger;

	public FeatureFlagEvaluatorTestFixture()
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

		Evaluator = new FeatureFlagEvaluator(Repository, evaluationManager, Cache);
	}

	public async Task DisposeAsync()
	{
		_redisConnection?.Dispose();
		await _postgresContainer.DisposeAsync();
		await _redisContainer.DisposeAsync();
	}

	public FeatureFlag CreateTestFlag(string key, FlagEvaluationMode evaluationMode)
	{
		var flag = new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for integration tests",
			AuditRecord = FlagAuditRecord.NewFlag("test-user")
		};
		flag.EvaluationModeSet.AddMode(evaluationMode);
		return flag;
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

	private async Task CreateFeatureFlagsTable(string connectionString)
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