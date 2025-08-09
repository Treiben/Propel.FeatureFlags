using FeatureRabbit.Flags.Cache.Redis;
using FeatureRabbit.Flags.Core;
using FeatureRabbit.Flags.Persistence.PostgresSQL;
using FeatureRabbit.Management.Api.Endpoints;
using FeatureRabbit.Management.Api.Endpoints.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using System.Security.Claims;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ApiTests.CreateFlagHandlerTests;

/* The tests cover these scenarios with CreateFlagHandler integration:
 *		Successful flag creation with all field types
 *		Duplicate key conflict handling
 *		Complex data serialization/deserialization
 *		Time-based field conversion (TimeOnly to TimeSpan)
 *		Cache invalidation after creation
 *		User context handling
 *		Error scenarios and exception handling
 *		Different flag status configurations
 *		Targeting rules and user lists
 *		Variations and tags handling
*/

public class CreateFlagHandler_SuccessfulCreation : IClassFixture<CreateFlagHandlerFixture>
{
	private readonly CreateFlagHandlerFixture _fixture;

	public CreateFlagHandler_SuccessfulCreation(CreateFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidRequest_ThenCreatesFlag()
	{
		// Arrange
		await _fixture.ClearAllData();
		var request = _fixture.CreateValidRequest("test-flag");

		// Act
		var result = await _fixture.Handler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Created<FeatureFlagDto>>();
		var createdResult = (Created<FeatureFlagDto>)result;
		createdResult.Location.ShouldBe("/api/flags/test-flag");
		createdResult.Value.ShouldNotBeNull();
		createdResult.Value.Key.ShouldBe("test-flag");
		createdResult.Value.Name.ShouldBe("Test Flag");
		createdResult.Value.CreatedBy.ShouldBe("test-user");
		createdResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify it's actually in the database
		var storedFlag = await _fixture.Repository.GetAsync("test-flag");
		storedFlag.ShouldNotBeNull();
		storedFlag.Key.ShouldBe("test-flag");
		storedFlag.Name.ShouldBe("Test Flag");
	}

	[Fact]
	public async Task If_FlagWithTimeWindows_ThenConvertsTimeOnlyCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var request = _fixture.CreateValidRequest("time-window-flag");
		request.WindowStartTime = new TimeOnly(9, 0); // 9 AM
		request.WindowEndTime = new TimeOnly(17, 30); // 5:30 PM
		request.TimeZone = "America/New_York";
		request.WindowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday };

		// Act
		var result = await _fixture.Handler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Created<FeatureFlagDto>>();
		var createdResult = (Created<FeatureFlagDto>)result;
		createdResult.Value.WindowStartTime.ShouldBe(new TimeOnly(9, 0));
		createdResult.Value.WindowEndTime.ShouldBe(new TimeOnly(17, 30));
		createdResult.Value.TimeZone.ShouldBe("America/New_York");
		createdResult.Value.WindowDays.ShouldContain(DayOfWeek.Monday);
		createdResult.Value.WindowDays.ShouldContain(DayOfWeek.Friday);

		// Verify database conversion
		var storedFlag = await _fixture.Repository.GetAsync("time-window-flag");
		storedFlag.ShouldNotBeNull();
		storedFlag.WindowStartTime.ShouldBe(TimeSpan.FromHours(9));
		storedFlag.WindowEndTime.ShouldBe(new TimeSpan(17, 30, 0));
	}

	[Fact]
	public async Task If_FlagWithTargetingRules_ThenStoresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var request = _fixture.CreateValidRequest("targeted-flag");
		request.TargetingRules = new List<TargetingRule>
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

		// Act
		var result = await _fixture.Handler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Created<FeatureFlagDto>>();
		var createdResult = (Created<FeatureFlagDto>)result;
		createdResult.Value.TargetingRules.Count.ShouldBe(2);
		createdResult.Value.TargetingRules[0].Attribute.ShouldBe("country");
		createdResult.Value.TargetingRules[0].Values.ShouldContain("US");
		createdResult.Value.TargetingRules[1].Variation.ShouldBe("premium-features");

		// Verify database storage
		var storedFlag = await _fixture.Repository.GetAsync("targeted-flag");
		storedFlag.ShouldNotBeNull();
		storedFlag.TargetingRules.Count.ShouldBe(2);
		storedFlag.TargetingRules[0].Attribute.ShouldBe("country");
	}

	[Fact]
	public async Task If_FlagWithVariationsAndUsers_ThenStoresAllData()
	{
		// Arrange
		await _fixture.ClearAllData();
		var request = _fixture.CreateValidRequest("complex-flag");
		request.EnabledUsers = new List<string> { "user1", "user2", "admin@company.com" };
		request.DisabledUsers = new List<string> { "blockeduser", "testuser" };
		request.Variations = new Dictionary<string, object>
		{
			{ "on", new { version = "v2", features = new[] { "feature1", "feature2" } } },
			{ "off", false },
			{ "beta", "beta-version" }
		};
		request.DefaultVariation = "beta";
		request.Tags = new Dictionary<string, string>
		{
			{ "team", "backend" },
			{ "environment", "production" },
			{ "priority", "high" }
		};

		// Act
		var result = await _fixture.Handler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Created<FeatureFlagDto>>();
		var createdResult = (Created<FeatureFlagDto>)result;
		
		createdResult.Value.EnabledUsers.Count.ShouldBe(3);
		createdResult.Value.EnabledUsers.ShouldContain("admin@company.com");
		createdResult.Value.DisabledUsers.Count.ShouldBe(2);
		createdResult.Value.Variations.Count.ShouldBe(3);
		createdResult.Value.DefaultVariation.ShouldBe("beta");
		createdResult.Value.Tags["team"].ShouldBe("backend");
		createdResult.Value.Tags["priority"].ShouldBe("high");

		// Verify database storage and complex object serialization
		var storedFlag = await _fixture.Repository.GetAsync("complex-flag");
		storedFlag.ShouldNotBeNull();
		storedFlag.EnabledUsers.ShouldContain("admin@company.com");
		storedFlag.Tags["environment"].ShouldBe("production");
	}
}

public class CreateFlagHandler_ConflictHandling : IClassFixture<CreateFlagHandlerFixture>
{
	private readonly CreateFlagHandlerFixture _fixture;

	public CreateFlagHandler_ConflictHandling(CreateFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagAlreadyExists_ThenReturnsConflict()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("existing-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = _fixture.CreateValidRequest("existing-flag");

		// Act
		var result = await _fixture.Handler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Conflict<string>>();
		var conflictResult = (Conflict<string>)result;
		conflictResult.Value.ShouldBe("Feature flag 'existing-flag' already exists");
	}

	[Fact]
	public async Task If_DuplicateAfterCreation_ThenSecondCallReturnsConflict()
	{
		// Arrange
		await _fixture.ClearAllData();
		var request = _fixture.CreateValidRequest("duplicate-test");

		// Act - First creation
		var firstResult = await _fixture.Handler.HandleAsync(request);
		var secondResult = await _fixture.Handler.HandleAsync(request);

		// Assert
		firstResult.ShouldBeOfType<Created<FeatureFlagDto>>();
		secondResult.ShouldBeOfType<Conflict<string>>();
	}
}

public class CreateFlagHandler_DefaultValues : IClassFixture<CreateFlagHandlerFixture>
{
	private readonly CreateFlagHandlerFixture _fixture;

	public CreateFlagHandler_DefaultValues(CreateFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_MinimalRequest_ThenAppliesCorrectDefaults()
	{
		// Arrange
		await _fixture.ClearAllData();
		var request = new CreateFeatureFlagRequest
		{
			Key = "minimal-flag",
			Name = "Minimal Flag"
			// All other fields should use defaults
		};

		// Act
		var result = await _fixture.Handler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Created<FeatureFlagDto>>();
		var createdResult = (Created<FeatureFlagDto>)result;
		
		createdResult.Value.Description.ShouldBe(string.Empty);
		createdResult.Value.Status.ShouldBe(FeatureFlagStatus.Disabled.ToString());
		createdResult.Value.PercentageEnabled.ShouldBe(0);
		createdResult.Value.IsPermanent.ShouldBeFalse();
		createdResult.Value.DefaultVariation.ShouldBe("off");
		createdResult.Value.Variations.Count.ShouldBe(2);
		createdResult.Value.Variations.ShouldContainKey("on");
		createdResult.Value.Variations.ShouldContainKey("off");
		createdResult.Value.TargetingRules.Count.ShouldBe(0);
		createdResult.Value.EnabledUsers.Count.ShouldBe(0);
		createdResult.Value.DisabledUsers.Count.ShouldBe(0);
		createdResult.Value.Tags.Count.ShouldBe(0);
	}

	[Fact]
	public async Task If_NullOptionalFields_ThenHandlesGracefully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var request = new CreateFeatureFlagRequest
		{
			Key = "null-fields-flag",
			Name = "Null Fields Test",
			Description = null,
			TargetingRules = null,
			EnabledUsers = null,
			DisabledUsers = null,
			Variations = null,
			DefaultVariation = null,
			Tags = null
		};

		// Act
		var result = await _fixture.Handler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Created<FeatureFlagDto>>();
		var createdResult = (Created<FeatureFlagDto>)result;
		
		createdResult.Value.Description.ShouldBe(string.Empty);
		createdResult.Value.DefaultVariation.ShouldBe("off");
		createdResult.Value.Variations.Count.ShouldBe(2); // Default "on"=true, "off"=false
	}
}

public class CreateFlagHandler_CacheInvalidation : IClassFixture<CreateFlagHandlerFixture>
{
	private readonly CreateFlagHandlerFixture _fixture;

	public CreateFlagHandler_CacheInvalidation(CreateFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagCreated_ThenInvalidatesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		var request = _fixture.CreateValidRequest("cache-test-flag");

		// Pre-populate cache with different data (simulate stale cache)
		var staleCacheFlag = _fixture.CreateTestFlag("cache-test-flag", FeatureFlagStatus.Enabled);
		await _fixture.Cache.SetAsync("cache-test-flag", staleCacheFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Created<FeatureFlagDto>>();

		// Verify cache was cleared (should be null after RemoveAsync)
		var cachedFlag = await _fixture.Cache.GetAsync("cache-test-flag");
		cachedFlag.ShouldBeNull();
	}
}

public class CreateFlagHandler_ErrorScenarios : IClassFixture<CreateFlagHandlerFixture>
{
	private readonly CreateFlagHandlerFixture _fixture;

	public CreateFlagHandler_ErrorScenarios(CreateFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_RepositoryThrowsException_ThenReturnsInternalServerError()
	{
		// Arrange
		await _fixture.ClearAllData();
		await _fixture.DisposeContainers(); // Force database connection issues

		var request = _fixture.CreateValidRequest("error-flag");

		// Act
		var result = await _fixture.Handler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<StatusCodeHttpResult>();
		var statusResult = (StatusCodeHttpResult)result;
		statusResult.StatusCode.ShouldBe(500);
	}

	[Fact]
	public async Task If_DatabaseConnectionFails_ThenLogsErrorAndReturns500()
	{
		// Arrange
		var badConnectionString = "Host=nonexistent;Database=fake;Username=fake;Password=fake";
		var badRepository = new PostgreSQLFeatureFlagRepository(badConnectionString, _fixture.MockRepositoryLogger.Object);
		var badHandler = new CreateFlagHandler(badRepository, _fixture.Cache, _fixture.MockLogger.Object, _fixture.CurrentUserService);

		var request = _fixture.CreateValidRequest("bad-connection-flag");

		// Act
		var result = await badHandler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<StatusCodeHttpResult>();
		var statusResult = (StatusCodeHttpResult)result;
		statusResult.StatusCode.ShouldBe(500);

		// Verify error was logged
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error creating feature flag")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class CreateFlagHandler_ScheduledFlags : IClassFixture<CreateFlagHandlerFixture>
{
	private readonly CreateFlagHandlerFixture _fixture;

	public CreateFlagHandler_ScheduledFlags(CreateFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ScheduledFlag_ThenStoresScheduledDates()
	{
		// Arrange
		await _fixture.ClearAllData();
		var request = _fixture.CreateValidRequest("scheduled-flag");
		request.Status = FeatureFlagStatus.Scheduled;
		request.ScheduledEnableDate = DateTime.UtcNow.AddDays(1);
		request.ScheduledDisableDate = DateTime.UtcNow.AddDays(30);
		request.ExpirationDate = DateTime.UtcNow.AddDays(90);

		// Act
		var result = await _fixture.Handler.HandleAsync(request);

		// Assert
		result.ShouldBeOfType<Created<FeatureFlagDto>>();
		var createdResult = (Created<FeatureFlagDto>)result;
		
		createdResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
		createdResult.Value.ScheduledEnableDate.ShouldNotBeNull();
		createdResult.Value.ScheduledDisableDate.ShouldNotBeNull();
		createdResult.Value.ExpirationDate.ShouldNotBeNull();

		// Verify precise date storage
		var storedFlag = await _fixture.Repository.GetAsync("scheduled-flag");
		storedFlag.ShouldNotBeNull();
		storedFlag.ScheduledEnableDate.Value.ShouldBeInRange(
			request.ScheduledEnableDate.Value.AddSeconds(-1),
			request.ScheduledEnableDate.Value.AddSeconds(1));
	}
}

public class CreateFlagHandlerFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public CreateFlagHandler Handler { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public CurrentUserService CurrentUserService { get; private set; } = null!;
	public Mock<ILogger<CreateFlagHandler>> MockLogger { get; }
	public Mock<ILogger<PostgreSQLFeatureFlagRepository>> MockRepositoryLogger { get; }
	
	private IConnectionMultiplexer _redisConnection = null!;

	public CreateFlagHandlerFixture()
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

		MockLogger = new Mock<ILogger<CreateFlagHandler>>();
		MockRepositoryLogger = new Mock<ILogger<PostgreSQLFeatureFlagRepository>>();
	}

	public async Task InitializeAsync()
	{
		// Start containers
		await _postgresContainer.StartAsync();
		await _redisContainer.StartAsync();

		// Setup PostgreSQL
		var postgresConnectionString = _postgresContainer.GetConnectionString();
		await CreatePostgresTables(postgresConnectionString);
		Repository = new PostgreSQLFeatureFlagRepository(postgresConnectionString, MockRepositoryLogger.Object);

		// Setup Redis
		var redisConnectionString = _redisContainer.GetConnectionString();
		_redisConnection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
		Cache = new RedisFeatureFlagCache(_redisConnection, new Mock<ILogger<RedisFeatureFlagCache>>().Object);

		// Setup CurrentUserService
		var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
		var mockHttpContext = new Mock<HttpContext>();
		var mockUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.Name, "test-user"),
			new Claim(ClaimTypes.NameIdentifier, "user-123")
		}, "test"));
		
		mockHttpContext.Setup(x => x.User).Returns(mockUser);
		mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);
		CurrentUserService = new CurrentUserService(mockHttpContextAccessor.Object);

		// Setup Handler
		Handler = new CreateFlagHandler(Repository, Cache, MockLogger.Object, CurrentUserService);
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

	public CreateFeatureFlagRequest CreateValidRequest(string key)
	{
		return new CreateFeatureFlagRequest
		{
			Key = key,
			Name = "Test Flag",
			Description = "Integration test flag",
			Status = FeatureFlagStatus.Disabled,
			PercentageEnabled = 0,
			IsPermanent = false
		};
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
	}
}