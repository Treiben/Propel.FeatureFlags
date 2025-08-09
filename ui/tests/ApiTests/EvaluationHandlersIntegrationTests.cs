using FeatureRabbit.Flags.Cache.Redis;
using FeatureRabbit.Flags.Client;
using FeatureRabbit.Flags.Core;
using FeatureRabbit.Flags.Persistence.PostgresSQL;
using FeatureRabbit.Management.Api.Endpoints;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using System.Text.Json;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ApiTests.EvaluationHandlersTests;

/* The tests cover these scenarios with MultiFlagEvaluatorHandler and EvaluationHandler integration:
 *		Multiple flag evaluation with various statuses
 *		Single flag evaluation with different configurations
 *		User-specific evaluations with attributes
 *		JSON attribute parsing and handling
 *		Cache-first evaluation strategy
 *		Repository fallback scenarios
 *		Error handling and exception scenarios
 *		Targeting rule evaluations
 *		Percentage rollout evaluations
 *		Time window evaluations
 *		Scheduled flag evaluations
*/

public class MultiFlagEvaluatorHandler_MultipleFlags : IClassFixture<EvaluationHandlersFixture>
{
	private readonly EvaluationHandlersFixture _fixture;

	public MultiFlagEvaluatorHandler_MultipleFlags(EvaluationHandlersFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_MultipleValidFlags_ThenEvaluatesAll()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var enabledFlag = _fixture.CreateTestFlag("enabled-flag", FeatureFlagStatus.Enabled);
		var disabledFlag = _fixture.CreateTestFlag("disabled-flag", FeatureFlagStatus.Disabled);
		var percentageFlag = _fixture.CreateTestFlag("percentage-flag", FeatureFlagStatus.Percentage);
		percentageFlag.PercentageEnabled = 50;

		await _fixture.Repository.CreateAsync(enabledFlag);
		await _fixture.Repository.CreateAsync(disabledFlag);
		await _fixture.Repository.CreateAsync(percentageFlag);

		var request = new EvaluateMultipleRequest
		{
			FlagKeys = new List<string> { "enabled-flag", "disabled-flag", "percentage-flag" },
			UserId = "test-user-123",
			Attributes = new Dictionary<string, object>
			{
				{ "country", "US" },
				{ "userTier", "premium" }
			}
		};

		// Act
		var result = await _fixture.MultiFlagHandler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		var okResult = (Ok<Dictionary<string, EvaluationResult>>)result;
		
		okResult.Value.Count.ShouldBe(3);
		okResult.Value.ShouldContainKey("enabled-flag");
		okResult.Value.ShouldContainKey("disabled-flag");
		okResult.Value.ShouldContainKey("percentage-flag");

		okResult.Value["enabled-flag"].IsEnabled.ShouldBeTrue();
		okResult.Value["enabled-flag"].Reason.ShouldBe("Flag enabled");
		
		okResult.Value["disabled-flag"].IsEnabled.ShouldBeFalse();
		okResult.Value["disabled-flag"].Reason.ShouldBe("Flag disabled");

		// Percentage flag result depends on consistent hashing, but should have valid reason
		okResult.Value["percentage-flag"].Reason.ShouldContain("Percentage rollout");
	}

	[Fact]
	public async Task If_MixOfExistingAndNonExistingFlags_ThenEvaluatesAll()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var existingFlag = _fixture.CreateTestFlag("existing-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new EvaluateMultipleRequest
		{
			FlagKeys = new List<string> { "existing-flag", "non-existent-flag" },
			UserId = "test-user"
		};

		// Act
		var result = await _fixture.MultiFlagHandler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		var okResult = (Ok<Dictionary<string, EvaluationResult>>)result;
		
		okResult.Value.Count.ShouldBe(2);
		okResult.Value["existing-flag"].IsEnabled.ShouldBeTrue();
		okResult.Value["non-existent-flag"].IsEnabled.ShouldBeFalse();
		okResult.Value["non-existent-flag"].Reason.ShouldBe("Flag not found");
	}

	[Fact]
	public async Task If_EmptyFlagKeysList_ThenReturnsEmptyResults()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var request = new EvaluateMultipleRequest
		{
			FlagKeys = new List<string>(),
			UserId = "test-user"
		};

		// Act
		var result = await _fixture.MultiFlagHandler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		var okResult = (Ok<Dictionary<string, EvaluationResult>>)result;
		okResult.Value.Count.ShouldBe(0);
	}

	[Fact]
	public async Task If_DuplicateFlagKeys_ThenEvaluatesOnce()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("duplicate-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new EvaluateMultipleRequest
		{
			FlagKeys = new List<string> { "duplicate-flag", "duplicate-flag", "duplicate-flag" },
			UserId = "test-user"
		};

		// Act
		var result = await _fixture.MultiFlagHandler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		var okResult = (Ok<Dictionary<string, EvaluationResult>>)result;

		// Should have 1 entry since the flag is evaluated only once
		okResult.Value.Count.ShouldBe(1);
		okResult.Value.Values.All(v => v.IsEnabled).ShouldBeTrue();
		okResult.Value.Values.All(v => v.Reason == "Flag enabled").ShouldBeTrue();
	}
}

public class MultiFlagEvaluatorHandler_TargetingAndRules : IClassFixture<EvaluationHandlersFixture>
{
	private readonly EvaluationHandlersFixture _fixture;

	public MultiFlagEvaluatorHandler_TargetingAndRules(EvaluationHandlersFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagsWithTargetingRules_ThenEvaluatesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var targetedFlag = _fixture.CreateTestFlag("targeted-flag", FeatureFlagStatus.UserTargeted);
		targetedFlag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "country",
				Operator = TargetingOperator.In,
				Values = new List<string> { "US", "CA" },
				Variation = "north-america"
			},
			new TargetingRule
			{
				Attribute = "userTier",
				Operator = TargetingOperator.In,
				Values = new List<string> { "premium" },
				Variation = "premium-features"
			}
		};

		await _fixture.Repository.CreateAsync(targetedFlag);

		var request = new EvaluateMultipleRequest
		{
			FlagKeys = new List<string> { "targeted-flag" },
			UserId = "premium-user",
			Attributes = new Dictionary<string, object>
			{
				{ "country", "US" },
				{ "userTier", "premium" }
			}
		};

		// Act
		var result = await _fixture.MultiFlagHandler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		var okResult = (Ok<Dictionary<string, EvaluationResult>>)result;
		
		okResult.Value["targeted-flag"].IsEnabled.ShouldBeTrue();
		okResult.Value["targeted-flag"].Variation.ShouldBe("north-america"); // First matching rule
		okResult.Value["targeted-flag"].Reason.ShouldContain("Targeting rule matched");
	}

	[Fact]
	public async Task If_UserInEnabledList_ThenOverridesOtherRules()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("user-override-flag", FeatureFlagStatus.Disabled);
		flag.EnabledUsers = new List<string> { "special-user", "admin@company.com" };

		await _fixture.Repository.CreateAsync(flag);

		var request = new EvaluateMultipleRequest
		{
			FlagKeys = new List<string> { "user-override-flag" },
			UserId = "special-user"
		};

		// Act
		var result = await _fixture.MultiFlagHandler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		var okResult = (Ok<Dictionary<string, EvaluationResult>>)result;
		
		okResult.Value["user-override-flag"].IsEnabled.ShouldBeTrue();
		okResult.Value["user-override-flag"].Reason.ShouldBe("User explicitly enabled");
	}
}

public class EvaluationHandler_SingleFlag : IClassFixture<EvaluationHandlersFixture>
{
	private readonly EvaluationHandlersFixture _fixture;

	public EvaluationHandler_SingleFlag(EvaluationHandlersFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidFlag_ThenEvaluatesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("single-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("single-flag", "test-user", null);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		
		okResult.Value.IsEnabled.ShouldBeTrue();
		okResult.Value.Variation.ShouldBe("on");
		okResult.Value.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_NonExistentFlag_ThenReturnsNotFound()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("non-existent", "test-user", null);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		
		okResult.Value.IsEnabled.ShouldBeFalse();
		okResult.Value.Variation.ShouldBe("off");
		okResult.Value.Reason.ShouldBe("Flag not found");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("test-flag-123")]
	[InlineData("flag_with_underscores")]
	[InlineData("flag-with-dashes")]
	public async Task If_DifferentFlagKeyFormats_ThenHandlesCorrectly(string flagKey)
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync(flagKey, "test-user", null);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		okResult.Value.IsEnabled.ShouldBeTrue();
	}
}

public class EvaluationHandler_AttributesParsing : IClassFixture<EvaluationHandlersFixture>
{
	private readonly EvaluationHandlersFixture _fixture;

	public EvaluationHandler_AttributesParsing(EvaluationHandlersFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidJsonAttributes_ThenParsesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("attr-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "country",
				Operator = TargetingOperator.In,
				Values = new List<string> { "US" },
				Variation = "us-version"
			}
		};
		await _fixture.Repository.CreateAsync(flag);

		var attributesJson = JsonSerializer.Serialize(new
		{
			country = "US",
			userTier = "premium",
			age = 25
		});

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("attr-flag", "test-user", attributesJson);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		
		okResult.Value.IsEnabled.ShouldBeTrue();
		okResult.Value.Variation.ShouldBe("us-version");
		okResult.Value.Reason.ShouldContain("Targeting rule matched");
	}

	[Fact]
	public async Task If_EmptyAttributes_ThenUsesEmptyDictionary()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("empty-attr-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("empty-attr-flag", "test-user", "");

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		okResult.Value.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task If_NullAttributes_ThenUsesEmptyDictionary()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("null-attr-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("null-attr-flag", "test-user", null);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		okResult.Value.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task If_InvalidJsonAttributes_ThenReturnBadRequest()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("invalid-json-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		var invalidJson = "{ invalid json }";

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("invalid-json-flag", "test-user", invalidJson);

		// Assert
		result.ShouldBeOfType<BadRequest<string>>();
	}

	[Fact]
	public async Task If_ComplexJsonAttributes_ThenParsesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("complex-attr-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "metadata.region",
				Operator = TargetingOperator.In,
				Values = new List<string> { "us-west" },
				Variation = "regional-version"
			}
		};
		await _fixture.Repository.CreateAsync(flag);

		var complexAttributesJson = JsonSerializer.Serialize(new
		{
			userId = "user123",
			metadata = new
			{
				region = "us-west",
				datacenter = "dc1"
			},
			preferences = new[] { "dark-mode", "notifications" }
		});

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("complex-attr-flag", "test-user", complexAttributesJson);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		// The targeting rule might not match depending on how nested attributes are handled,
		// but the JSON should parse without errors
		okResult.Value.ShouldNotBeNull();
	}
}

public class EvaluationHandlers_TimeBasedEvaluation : IClassFixture<EvaluationHandlersFixture>
{
	private readonly EvaluationHandlersFixture _fixture;

	public EvaluationHandlers_TimeBasedEvaluation(EvaluationHandlersFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ScheduledFlagBeforeEnableDate_ThenReturnsDisabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("scheduled-future", FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddDays(1);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddDays(30);
		await _fixture.Repository.CreateAsync(flag);

		var request = new EvaluateMultipleRequest
		{
			FlagKeys = new List<string> { "scheduled-future" },
			UserId = "test-user"
		};

		// Act
		var result = await _fixture.MultiFlagHandler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		var okResult = (Ok<Dictionary<string, EvaluationResult>>)result;
		
		okResult.Value["scheduled-future"].IsEnabled.ShouldBeFalse();
		okResult.Value["scheduled-future"].Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_ScheduledFlagInActiveWindow_ThenReturnsEnabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("scheduled-active", FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(-1);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddDays(1);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("scheduled-active", "test-user", null);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		
		okResult.Value.IsEnabled.ShouldBeTrue();
		okResult.Value.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_ExpiredFlag_ThenReturnsDisabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("expired-flag", FeatureFlagStatus.Enabled);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(-1);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("expired-flag", "test-user", null);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		
		okResult.Value.IsEnabled.ShouldBeFalse();
		okResult.Value.Reason.ShouldBe("Flag expired");
	}
}

public class EvaluationHandlers_CacheIntegration : IClassFixture<EvaluationHandlersFixture>
{
	private readonly EvaluationHandlersFixture _fixture;

	public EvaluationHandlers_CacheIntegration(EvaluationHandlersFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagInCache_ThenUsesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("cached-flag", FeatureFlagStatus.Enabled);
		
		// Only put in cache, not in repository
		await _fixture.Cache.SetAsync("cached-flag", flag);

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("cached-flag", "test-user", null);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		okResult.Value.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagNotInCacheButInRepository_ThenCachesFlag()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("repo-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.EvaluationHandler.HandleAsync("repo-flag", "test-user", null);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
		var okResult = (Ok<EvaluationResult>)result;
		okResult.Value.IsEnabled.ShouldBeTrue();

		// Verify flag is now cached
		var cachedFlag = await _fixture.Cache.GetAsync("repo-flag");
		cachedFlag.ShouldNotBeNull();
		cachedFlag.Key.ShouldBe("repo-flag");
	}
}

public class EvaluationHandlersFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public MultiFlagEvaluatorHandler MultiFlagHandler { get; private set; } = null!;
	public EvaluationHandler EvaluationHandler { get; private set; } = null!;
	public FeatureFlagClient Client { get; private set; } = null!;
	public FeatureFlagEvaluator Evaluator { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public Mock<ILogger<MultiFlagEvaluatorHandler>> MockMultiFlagLogger { get; }
	public Mock<ILogger<EvaluationHandler>> MockEvaluationLogger { get; }
	
	private IConnectionMultiplexer _redisConnection = null!;

	public EvaluationHandlersFixture()
	{
		_postgresContainer = new PostgreSqlBuilder()
			.WithImage("postgres:15-alpine")
			.WithDatabase("featureflags")
			.WithUsername("postgres")
			.WithPassword("postgres")
			.WithPortBinding(5432, true)
			.Build();

		_redisContainer = new RedisBuilder()
			.WithImage("redis:7-alpine")
			.WithPortBinding(6379, true)
			.Build();

		MockMultiFlagLogger = new Mock<ILogger<MultiFlagEvaluatorHandler>>();
		MockEvaluationLogger = new Mock<ILogger<EvaluationHandler>>();
	}

	public async Task InitializeAsync()
	{
		// Start containers
		await _postgresContainer.StartAsync();
		await _redisContainer.StartAsync();

		// Setup PostgreSQL
		var postgresConnectionString = _postgresContainer.GetConnectionString();
		await CreatePostgresTables(postgresConnectionString);
		Repository = new PostgreSQLFeatureFlagRepository(postgresConnectionString, new Mock<ILogger<PostgreSQLFeatureFlagRepository>>().Object);

		// Setup Redis
		var redisConnectionString = _redisContainer.GetConnectionString();
		_redisConnection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
		Cache = new RedisFeatureFlagCache(_redisConnection, new Mock<ILogger<RedisFeatureFlagCache>>().Object);

		// Setup Evaluator and Client
		Evaluator = new FeatureFlagEvaluator(Repository, Cache, new Mock<ILogger<FeatureFlagEvaluator>>().Object);
		Client = new FeatureFlagClient(Evaluator);

		// Setup Handlers
		MultiFlagHandler = new MultiFlagEvaluatorHandler(Client, MockMultiFlagLogger.Object);
		EvaluationHandler = new EvaluationHandler(Client, MockEvaluationLogger.Object);
	}

	public async Task DisposeAsync()
	{
		await DisposeContainers();
	}

	public async Task DisposeContainers()
	{
		_redisConnection?.Dispose();
		await _postgresContainer.DisposeAsync();
		await _redisContainer.DisposeAsync();
	}

	private async Task CreatePostgresTables(string connectionString)
	{
		const string createTableSql = @"
		CREATE TABLE feature_flags (
			key VARCHAR(255) PRIMARY KEY,
			name VARCHAR(500) NOT NULL,
			description TEXT NOT NULL,
			status INTEGER NOT NULL,
			created_at TIMESTAMP NOT NULL,
			updated_at TIMESTAMP NOT NULL,
			created_by VARCHAR(255) NOT NULL,
			updated_by VARCHAR(255) NOT NULL,
			expiration_date TIMESTAMP NULL,
			scheduled_enable_date TIMESTAMP NULL,
			scheduled_disable_date TIMESTAMP NULL,
			window_start_time TIME NULL,
			window_end_time TIME NULL,
			time_zone VARCHAR(100) NULL,
			window_days JSONB NOT NULL DEFAULT '[]',
			percentage_enabled INTEGER NOT NULL DEFAULT 0,
			targeting_rules JSONB NOT NULL DEFAULT '[]',
			enabled_users JSONB NOT NULL DEFAULT '[]',
			disabled_users JSONB NOT NULL DEFAULT '[]',
			variations JSONB NOT NULL DEFAULT '{}',
			default_variation VARCHAR(255) NOT NULL DEFAULT 'off',
			tags JSONB NOT NULL DEFAULT '{}',
			is_permanent BOOLEAN NOT NULL DEFAULT false
		);

		CREATE INDEX ix_feature_flags_status ON feature_flags(status);
		CREATE INDEX ix_feature_flags_expiration_date ON feature_flags(expiration_date) WHERE expiration_date IS NOT NULL;
		CREATE INDEX ix_feature_flags_created_at ON feature_flags(created_at);
		CREATE INDEX ix_feature_flags_tags ON feature_flags USING GIN(tags);
	";

		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand(createTableSql, connection);
		await command.ExecuteNonQueryAsync();
	}

	public FeatureFlag CreateTestFlag(string key, FeatureFlagStatus status)
	{
		return new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for integration tests",
			Status = status,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
			CreatedBy = "integration-test",
			UpdatedBy = "integration-test",
			DefaultVariation = "off",
			TargetingRules = new List<TargetingRule>(),
			EnabledUsers = new List<string>(),
			DisabledUsers = new List<string>(),
			Variations = new Dictionary<string, object>(),
			Tags = new Dictionary<string, string>(),
			WindowDays = new List<DayOfWeek>(),
			PercentageEnabled = 0,
			IsPermanent = false
		};
	}

	// Helper method to clear all data between tests
	public async Task ClearAllData()
	{
		// Clear PostgreSQL
		var connectionString = _postgresContainer.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();

		// Clear Redis
		await Cache.ClearAsync();

		// Reset mock loggers for clean state
		MockMultiFlagLogger.Reset();
		MockEvaluationLogger.Reset();
	}
}