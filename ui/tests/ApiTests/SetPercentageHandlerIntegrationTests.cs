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

namespace ApiTests.SetPercentageHandlerTests;

/* The tests cover these scenarios with SetPercentageHandler integration:
 *		Successful percentage setting with status change to Percentage
 *		Various percentage values (0, 50, 100)
 *		Non-existent flag handling (404)
 *		Status change from different initial statuses
 *		Cache invalidation after percentage update
 *		User context tracking and logging
 *		Database persistence verification
 *		Error scenarios and exception handling
 *		Existing percentage flag updates
 *		Complex flag data preservation during percentage update
 *		Logging verification for audit trails
 *		Edge case percentage values
*/

public class SetPercentageHandler_SuccessfulPercentageSetting : IClassFixture<SetPercentageHandlerFixture>
{
	private readonly SetPercentageHandlerFixture _fixture;

	public SetPercentageHandler_SuccessfulPercentageSetting(SetPercentageHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidRequest_ThenSetsPercentageAndStatus()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("percentage-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(75);

		// Act
		var result = await _fixture.Handler.HandleAsync("percentage-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Key.ShouldBe("percentage-flag");
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Percentage.ToString());
		okResult.Value.PercentageEnabled.ShouldBe(75);
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("percentage-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Percentage);
		updatedFlag.PercentageEnabled.ShouldBe(75);
		updatedFlag.UpdatedBy.ShouldBe("test-user");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(25)]
	[InlineData(50)]
	[InlineData(75)]
	[InlineData(100)]
	public async Task If_VariousPercentageValues_ThenSetsCorrectly(int percentage)
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag($"flag-{percentage}", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(percentage);

		// Act
		var result = await _fixture.Handler.HandleAsync($"flag-{percentage}", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Percentage.ToString());
		okResult.Value.PercentageEnabled.ShouldBe(percentage);

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync($"flag-{percentage}");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.PercentageEnabled.ShouldBe(percentage);
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	public async Task If_DifferentOriginalStatuses_ThenChangesToPercentage(FeatureFlagStatus originalStatus)
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag($"status-{originalStatus}", originalStatus);
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(60);

		// Act
		var result = await _fixture.Handler.HandleAsync($"status-{originalStatus}", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Percentage.ToString());
		okResult.Value.PercentageEnabled.ShouldBe(60);

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync($"status-{originalStatus}");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Percentage);
	}

	[Fact]
	public async Task If_AlreadyPercentageFlag_ThenUpdatesPercentage()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("existing-percentage-flag", FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 30;
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(85);

		// Act
		var result = await _fixture.Handler.HandleAsync("existing-percentage-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Percentage.ToString());
		okResult.Value.PercentageEnabled.ShouldBe(85);

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("existing-percentage-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.PercentageEnabled.ShouldBe(85);
	}

	[Fact]
	public async Task If_ComplexFlagWithTargeting_ThenPreservesDataAndSetsPercentage()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("complex-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "country",
				Operator = TargetingOperator.In,
				Values = new List<string> { "US", "CA" },
				Variation = "north-america"
			}
		};
		flag.EnabledUsers = new List<string> { "user1", "user2" };
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium" },
			{ "off", "basic" }
		};
		flag.Tags = new Dictionary<string, string>
		{
			{ "team", "backend" },
			{ "priority", "high" }
		};
		flag.ExpirationDate = DateTime.UtcNow.AddDays(30);
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(40);

		// Act
		var result = await _fixture.Handler.HandleAsync("complex-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Percentage.ToString());
		okResult.Value.PercentageEnabled.ShouldBe(40);
		okResult.Value.TargetingRules.Count.ShouldBe(1); // Preserved
		okResult.Value.EnabledUsers.Count.ShouldBe(2); // Preserved
		okResult.Value.Variations.Count.ShouldBe(2); // Preserved
		okResult.Value.Tags.Count.ShouldBe(2); // Preserved
		okResult.Value.ExpirationDate.ShouldNotBeNull(); // Preserved

		// Verify database persistence of all data
		var updatedFlag = await _fixture.Repository.GetAsync("complex-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Percentage);
		updatedFlag.PercentageEnabled.ShouldBe(40);
		updatedFlag.TargetingRules.Count.ShouldBe(1);
		updatedFlag.EnabledUsers.Count.ShouldBe(2);
		updatedFlag.ExpirationDate.ShouldNotBeNull();
	}
}

public class SetPercentageHandler_EdgeCases : IClassFixture<SetPercentageHandlerFixture>
{
	private readonly SetPercentageHandlerFixture _fixture;

	public SetPercentageHandler_EdgeCases(SetPercentageHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ZeroPercentage_ThenSetsCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("zero-percentage-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(0);

		// Act
		var result = await _fixture.Handler.HandleAsync("zero-percentage-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Percentage.ToString());
		okResult.Value.PercentageEnabled.ShouldBe(0);
	}

	[Fact]
	public async Task If_HundredPercentage_ThenSetsCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("hundred-percentage-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(100);

		// Act
		var result = await _fixture.Handler.HandleAsync("hundred-percentage-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Percentage.ToString());
		okResult.Value.PercentageEnabled.ShouldBe(100);
	}

	[Fact]
	public async Task If_SamePercentageSet_ThenUpdatesSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("same-percentage-flag", FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 50;
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(50);

		// Act
		var result = await _fixture.Handler.HandleAsync("same-percentage-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Percentage.ToString());
		okResult.Value.PercentageEnabled.ShouldBe(50);
		okResult.Value.UpdatedBy.ShouldBe("test-user"); // Should still update user
	}

	[Fact]
	public async Task If_PermanentFlag_ThenSetsPercentageSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("permanent-flag", FeatureFlagStatus.Enabled);
		flag.IsPermanent = true;
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(25);

		// Act
		var result = await _fixture.Handler.HandleAsync("permanent-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Percentage.ToString());
		okResult.Value.PercentageEnabled.ShouldBe(25);
		okResult.Value.IsPermanent.ShouldBeTrue(); // Preserved

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("permanent-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.IsPermanent.ShouldBeTrue();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Percentage);
	}
}

public class SetPercentageHandler_NotFoundScenarios : IClassFixture<SetPercentageHandlerFixture>
{
	private readonly SetPercentageHandlerFixture _fixture;

	public SetPercentageHandler_NotFoundScenarios(SetPercentageHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await _fixture.ClearAllData();

		var request = new SetPercentageRequest(50);

		// Act
		var result = await _fixture.Handler.HandleAsync("non-existent-flag", request);

		// Assert
		result.ShouldBeOfType<NotFound<string>>();
		var notFoundResult = (NotFound<string>)result;
		notFoundResult.Value.ShouldBe("Feature flag 'non-existent-flag' not found");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("missing-flag")]
	[InlineData("never-created")]
	public async Task If_VariousNonExistentKeys_ThenReturnsNotFound(string key)
	{
		// Arrange
		await _fixture.ClearAllData();

		var request = new SetPercentageRequest(75);

		// Act
		var result = await _fixture.Handler.HandleAsync(key, request);

		// Assert
		result.ShouldBeOfType<NotFound<string>>();
		var notFoundResult = (NotFound<string>)result;
		notFoundResult.Value.ShouldBe($"Feature flag '{key}' not found");
	}
}

public class SetPercentageHandler_CacheInvalidation : IClassFixture<SetPercentageHandlerFixture>
{
	private readonly SetPercentageHandlerFixture _fixture;

	public SetPercentageHandler_CacheInvalidation(SetPercentageHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_PercentageSet_ThenInvalidatesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("cached-percentage-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Pre-populate cache
		await _fixture.Cache.SetAsync("cached-percentage-flag", flag);
		var cachedFlag = await _fixture.Cache.GetAsync("cached-percentage-flag");
		cachedFlag.ShouldNotBeNull(); // Verify cache is populated

		var request = new SetPercentageRequest(80);

		// Act
		var result = await _fixture.Handler.HandleAsync("cached-percentage-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify cache was cleared
		var cacheAfterUpdate = await _fixture.Cache.GetAsync("cached-percentage-flag");
		cacheAfterUpdate.ShouldBeNull();
	}

	[Fact]
	public async Task If_NonExistentFlagPercentageSet_ThenNoCacheOperation()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Pre-populate cache with unrelated flag
		var unrelatedFlag = _fixture.CreateTestFlag("unrelated-flag", FeatureFlagStatus.Enabled);
		await _fixture.Cache.SetAsync("unrelated-flag", unrelatedFlag);

		var request = new SetPercentageRequest(60);

		// Act
		var result = await _fixture.Handler.HandleAsync("non-existent-flag", request);

		// Assert
		result.ShouldBeOfType<NotFound<string>>();

		// Verify unrelated cache entry is unchanged
		var cachedFlag = await _fixture.Cache.GetAsync("unrelated-flag");
		cachedFlag.ShouldNotBeNull();
		cachedFlag.Key.ShouldBe("unrelated-flag");
	}
}

public class SetPercentageHandler_AuditLogging : IClassFixture<SetPercentageHandlerFixture>
{
	private readonly SetPercentageHandlerFixture _fixture;

	public SetPercentageHandler_AuditLogging(SetPercentageHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_SuccessfulPercentageSet_ThenLogsWithCorrectDetails()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("audit-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(65);

		// Act
		var result = await _fixture.Handler.HandleAsync("audit-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify specific log message structure
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => 
					v.ToString()!.Contains("Feature flag audit-flag percentage set to 65% by test-user")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task If_MultiplePercentageUpdates_ThenLogsEachOperation()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag1 = _fixture.CreateTestFlag("percentage-flag-1", FeatureFlagStatus.Enabled);
		var flag2 = _fixture.CreateTestFlag("percentage-flag-2", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);

		var request1 = new SetPercentageRequest(30);
		var request2 = new SetPercentageRequest(70);

		// Act
		await _fixture.Handler.HandleAsync("percentage-flag-1", request1);
		await _fixture.Handler.HandleAsync("percentage-flag-2", request2);

		// Assert
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("percentage-flag-1 percentage set to 30%")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("percentage-flag-2 percentage set to 70%")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(99)]
	[InlineData(100)]
	public async Task If_DifferentPercentageValues_ThenLogsCorrectPercentage(int percentage)
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag($"log-test-{percentage}", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new SetPercentageRequest(percentage);

		// Act
		var result = await _fixture.Handler.HandleAsync($"log-test-{percentage}", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"percentage set to {percentage}%")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class SetPercentageHandler_ErrorScenarios : IClassFixture<SetPercentageHandlerFixture>
{
	private readonly SetPercentageHandlerFixture _fixture;

	public SetPercentageHandler_ErrorScenarios(SetPercentageHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_RepositoryThrowsException_ThenReturnsInternalServerError()
	{
		// Arrange
		await _fixture.ClearAllData();
		await _fixture.DisposeContainers();

		var request = new SetPercentageRequest(50);

		// Act
		var result = await _fixture.Handler.HandleAsync("error-flag", request);

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
		var badHandler = new SetPercentageHandler(badRepository, _fixture.Cache, _fixture.MockLogger.Object, _fixture.CurrentUserService);

		var request = new SetPercentageRequest(75);

		// Act
		var result = await badHandler.HandleAsync("bad-connection-flag", request);

		// Assert
		result.ShouldBeOfType<StatusCodeHttpResult>();
		var statusResult = (StatusCodeHttpResult)result;
		statusResult.StatusCode.ShouldBe(500);

		// Verify error was logged
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error setting percentage for feature flag bad-connection-flag")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class SetPercentageHandlerFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public SetPercentageHandler Handler { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public CurrentUserService CurrentUserService { get; private set; } = null!;
	public Mock<ILogger<SetPercentageHandler>> MockLogger { get; }
	public Mock<ILogger<PostgreSQLFeatureFlagRepository>> MockRepositoryLogger { get; }
	
	private IConnectionMultiplexer _redisConnection = null!;

	public SetPercentageHandlerFixture()
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

		MockLogger = new Mock<ILogger<SetPercentageHandler>>();
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
		Handler = new SetPercentageHandler(Repository, Cache, MockLogger.Object, CurrentUserService);
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

	public async Task ClearAllData()
	{
		var connectionString = _postgresContainer.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();

		await Cache.ClearAsync();
		MockLogger.Reset();
	}
}