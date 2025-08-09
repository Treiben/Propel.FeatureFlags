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

namespace ApiTests.DeleteFlagHandlerTests;

/* The tests cover these scenarios with DeleteFlagHandler integration:
 *		Successful flag deletion with verification
 *		Non-existent flag handling (404)
 *		Permanent flag deletion prevention (400)
 *		Cache invalidation after deletion
 *		User context logging
 *		Database transaction verification
 *		Error scenarios and exception handling
 *		Various flag types and statuses
 *		Logging verification for audit trails
*/

public class DeleteFlagHandler_SuccessfulDeletion : IClassFixture<DeleteFlagHandlerFixture>
{
	private readonly DeleteFlagHandlerFixture _fixture;

	public DeleteFlagHandler_SuccessfulDeletion(DeleteFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidFlagExists_ThenDeletesSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("deletable-flag", FeatureFlagStatus.Enabled);
		flag.IsPermanent = false;
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("deletable-flag");

		// Assert
		result.ShouldBeOfType<NoContent>();

		// Verify flag is actually deleted from database
		var deletedFlag = await _fixture.Repository.GetAsync("deletable-flag");
		deletedFlag.ShouldBeNull();

		// Verify logging occurred
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Feature flag deletable-flag deleted by test-user")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task If_DisabledFlag_ThenDeletesSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("disabled-flag", FeatureFlagStatus.Disabled);
		flag.IsPermanent = false;
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("disabled-flag");

		// Assert
		result.ShouldBeOfType<NoContent>();
		var deletedFlag = await _fixture.Repository.GetAsync("disabled-flag");
		deletedFlag.ShouldBeNull();
	}

	[Fact]
	public async Task If_ScheduledFlag_ThenDeletesSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("scheduled-flag", FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddDays(1);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddDays(30);
		flag.IsPermanent = false;
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("scheduled-flag");

		// Assert
		result.ShouldBeOfType<NoContent>();
		var deletedFlag = await _fixture.Repository.GetAsync("scheduled-flag");
		deletedFlag.ShouldBeNull();
	}

	[Fact]
	public async Task If_ComplexFlagWithTargeting_ThenDeletesSuccessfully()
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
		flag.DisabledUsers = new List<string> { "user3" };
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
		flag.IsPermanent = false;
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("complex-flag");

		// Assert
		result.ShouldBeOfType<NoContent>();
		var deletedFlag = await _fixture.Repository.GetAsync("complex-flag");
		deletedFlag.ShouldBeNull();
	}
}

public class DeleteFlagHandler_NotFoundScenarios : IClassFixture<DeleteFlagHandlerFixture>
{
	private readonly DeleteFlagHandlerFixture _fixture;

	public DeleteFlagHandler_NotFoundScenarios(DeleteFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Act
		var result = await _fixture.Handler.HandleAsync("non-existent-flag");

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

		// Act
		var result = await _fixture.Handler.HandleAsync(key);

		// Assert
		result.ShouldBeOfType<NotFound<string>>();
		var notFoundResult = (NotFound<string>)result;
		notFoundResult.Value.ShouldBe($"Feature flag '{key}' not found");
	}

	[Fact]
	public async Task If_FlagDeletedTwice_ThenSecondCallReturnsNotFound()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("once-deletable", FeatureFlagStatus.Enabled);
		flag.IsPermanent = false;
		await _fixture.Repository.CreateAsync(flag);

		// Act - First deletion
		var firstResult = await _fixture.Handler.HandleAsync("once-deletable");
		var secondResult = await _fixture.Handler.HandleAsync("once-deletable");

		// Assert
		firstResult.ShouldBeOfType<NoContent>();
		secondResult.ShouldBeOfType<NotFound<string>>();
	}
}

public class DeleteFlagHandler_PermanentFlagProtection : IClassFixture<DeleteFlagHandlerFixture>
{
	private readonly DeleteFlagHandlerFixture _fixture;

	public DeleteFlagHandler_PermanentFlagProtection(DeleteFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_PermanentFlag_ThenReturnsBadRequest()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("permanent-flag", FeatureFlagStatus.Enabled);
		flag.IsPermanent = true;
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("permanent-flag");

		// Assert
		result.ShouldBeOfType<BadRequest<string>>();
		var badRequestResult = (BadRequest<string>)result;
		badRequestResult.Value.ShouldBe("Cannot delete permanent feature flag");

		// Verify flag still exists in database
		var existingFlag = await _fixture.Repository.GetAsync("permanent-flag");
		existingFlag.ShouldNotBeNull();
		existingFlag.Key.ShouldBe("permanent-flag");
		existingFlag.IsPermanent.ShouldBeTrue();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.Percentage)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	public async Task If_PermanentFlagWithDifferentStatuses_ThenAlwaysReturnsBadRequest(FeatureFlagStatus status)
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag($"permanent-{status}", status);
		flag.IsPermanent = true;
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync($"permanent-{status}");

		// Assert
		result.ShouldBeOfType<BadRequest<string>>();

		// Verify flag still exists
		var existingFlag = await _fixture.Repository.GetAsync($"permanent-{status}");
		existingFlag.ShouldNotBeNull();
		existingFlag.Status.ShouldBe(status);
		existingFlag.IsPermanent.ShouldBeTrue();
	}
}

public class DeleteFlagHandler_CacheInvalidation : IClassFixture<DeleteFlagHandlerFixture>
{
	private readonly DeleteFlagHandlerFixture _fixture;

	public DeleteFlagHandler_CacheInvalidation(DeleteFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagDeleted_ThenInvalidatesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("cached-flag", FeatureFlagStatus.Enabled);
		flag.IsPermanent = false;
		await _fixture.Repository.CreateAsync(flag);

		// Pre-populate cache
		await _fixture.Cache.SetAsync("cached-flag", flag);
		var cachedFlag = await _fixture.Cache.GetAsync("cached-flag");
		cachedFlag.ShouldNotBeNull(); // Verify cache is populated

		// Act
		var result = await _fixture.Handler.HandleAsync("cached-flag");

		// Assert
		result.ShouldBeOfType<NoContent>();

		// Verify cache was cleared
		var cacheAfterDelete = await _fixture.Cache.GetAsync("cached-flag");
		cacheAfterDelete.ShouldBeNull();
	}

	[Fact]
	public async Task If_PermanentFlagNotDeleted_ThenCacheNotCleared()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("permanent-cached-flag", FeatureFlagStatus.Enabled);
		flag.IsPermanent = true;
		await _fixture.Repository.CreateAsync(flag);

		// Pre-populate cache
		await _fixture.Cache.SetAsync("permanent-cached-flag", flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("permanent-cached-flag");

		// Assert
		result.ShouldBeOfType<BadRequest<string>>();

		// Verify cache still contains the flag (since deletion was prevented)
		var cachedFlag = await _fixture.Cache.GetAsync("permanent-cached-flag");
		cachedFlag.ShouldNotBeNull();
		cachedFlag.Key.ShouldBe("permanent-cached-flag");
	}

	[Fact]
	public async Task If_NonExistentFlag_ThenNoCacheOperationPerformed()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Pre-populate cache with unrelated flag
		var unrelatedFlag = _fixture.CreateTestFlag("unrelated-flag", FeatureFlagStatus.Enabled);
		await _fixture.Cache.SetAsync("unrelated-flag", unrelatedFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync("non-existent-flag");

		// Assert
		result.ShouldBeOfType<NotFound<string>>();

		// Verify unrelated cache entry is unchanged
		var cachedFlag = await _fixture.Cache.GetAsync("unrelated-flag");
		cachedFlag.ShouldNotBeNull();
		cachedFlag.Key.ShouldBe("unrelated-flag");
	}
}

public class DeleteFlagHandler_ErrorScenarios : IClassFixture<DeleteFlagHandlerFixture>
{
	private readonly DeleteFlagHandlerFixture _fixture;

	public DeleteFlagHandler_ErrorScenarios(DeleteFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_RepositoryThrowsException_ThenReturnsInternalServerError()
	{
		// Arrange
		await _fixture.ClearAllData();
		await _fixture.DisposeContainers(); // Force database connection issues

		// Act
		var result = await _fixture.Handler.HandleAsync("error-flag");

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
		var badHandler = new DeleteFlagHandler(_fixture.CurrentUserService, badRepository, _fixture.Cache, _fixture.MockLogger.Object);

		// Act
		var result = await badHandler.HandleAsync("bad-connection-flag");

		// Assert
		result.ShouldBeOfType<StatusCodeHttpResult>();
		var statusResult = (StatusCodeHttpResult)result;
		statusResult.StatusCode.ShouldBe(500);

		// Verify error was logged
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error deleting feature flag bad-connection-flag")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class DeleteFlagHandler_AuditLogging : IClassFixture<DeleteFlagHandlerFixture>
{
	private readonly DeleteFlagHandlerFixture _fixture;

	public DeleteFlagHandler_AuditLogging(DeleteFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_SuccessfulDeletion_ThenLogsWithCorrectUserAndFlag()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("audit-flag", FeatureFlagStatus.Enabled);
		flag.IsPermanent = false;
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("audit-flag");

		// Assert
		result.ShouldBeOfType<NoContent>();

		// Verify specific log message structure
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => 
					v.ToString()!.Contains("Feature flag audit-flag deleted by test-user")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task If_MultipleDeletions_ThenLogsEachOperation()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag1 = _fixture.CreateTestFlag("audit-flag-1", FeatureFlagStatus.Enabled);
		var flag2 = _fixture.CreateTestFlag("audit-flag-2", FeatureFlagStatus.Disabled);
		flag1.IsPermanent = false;
		flag2.IsPermanent = false;
		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);

		// Act
		await _fixture.Handler.HandleAsync("audit-flag-1");
		await _fixture.Handler.HandleAsync("audit-flag-2");

		// Assert
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("audit-flag-1")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("audit-flag-2")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class DeleteFlagHandlerFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public DeleteFlagHandler Handler { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public CurrentUserService CurrentUserService { get; private set; } = null!;
	public Mock<ILogger<DeleteFlag>> MockLogger { get; }
	public Mock<ILogger<PostgreSQLFeatureFlagRepository>> MockRepositoryLogger { get; }
	
	private IConnectionMultiplexer _redisConnection = null!;

	public DeleteFlagHandlerFixture()
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

		MockLogger = new Mock<ILogger<DeleteFlag>>();
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
		Handler = new DeleteFlagHandler(CurrentUserService, Repository, Cache, MockLogger.Object);
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

		// Reset mock logger for clean state
		MockLogger.Reset();
	}
}