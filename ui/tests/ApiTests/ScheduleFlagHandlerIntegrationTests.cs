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

namespace ApiTests.ScheduleFlagHandlerTests;

/* The tests cover these scenarios with ScheduleFlagHandler integration:
 *		Successful flag scheduling with enable and disable dates
 *		Future enable date scheduling
 *		Enable date only scheduling (no disable date)
 *		Status change to Scheduled after scheduling
 *		Non-existent flag handling (404)
 *		Cache invalidation after scheduling
 *		User context tracking and logging
 *		Database persistence verification
 *		Error scenarios and exception handling
 *		Various flag statuses being scheduled
 *		Date validation and edge cases
 *		Logging verification for audit trails
*/

public class ScheduleFlagHandler_SuccessfulScheduling : IClassFixture<ScheduleFlagHandlerFixture>
{
	private readonly ScheduleFlagHandlerFixture _fixture;

	public ScheduleFlagHandler_SuccessfulScheduling(ScheduleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidRequest_ThenSchedulesFlag()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("schedule-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

		// Act
		var result = await _fixture.Handler.HandleAsync("schedule-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Key.ShouldBe("schedule-flag");
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
		okResult.Value.ScheduledEnableDate.ShouldNotBeNull();
		okResult.Value.ScheduledDisableDate.ShouldNotBeNull();
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("schedule-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Scheduled);
		updatedFlag.ScheduledEnableDate.Value.ShouldBeInRange(
			request.EnableDate.AddSeconds(-1),
			request.EnableDate.AddSeconds(1));
		updatedFlag.ScheduledDisableDate.Value.ShouldBeInRange(
			request.DisableDate.Value.AddSeconds(-1),
			request.DisableDate.Value.AddSeconds(1));
	}

	[Fact]
	public async Task If_EnableDateOnly_ThenSchedulesWithoutDisableDate()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("enable-only-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddHours(2),
			null
		);

		// Act
		var result = await _fixture.Handler.HandleAsync("enable-only-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
		okResult.Value.ScheduledEnableDate.ShouldNotBeNull();
		okResult.Value.ScheduledDisableDate.ShouldBeNull();

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("enable-only-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.ScheduledEnableDate.ShouldNotBeNull();
		updatedFlag.ScheduledDisableDate.ShouldBe(DateTime.MinValue);
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Percentage)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	public async Task If_DifferentOriginalStatuses_ThenChangesToScheduled(FeatureFlagStatus originalStatus)
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag($"status-{originalStatus}", originalStatus);
		await _fixture.Repository.CreateAsync(flag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(5)
		);

		// Act
		var result = await _fixture.Handler.HandleAsync($"status-{originalStatus}", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync($"status-{originalStatus}");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Scheduled);
	}

	[Fact]
	public async Task If_AlreadyScheduledFlag_ThenUpdatesSchedule()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("reschedule-flag", FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddDays(3);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddDays(10);
		await _fixture.Repository.CreateAsync(flag);

		var newRequest = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(2),
			DateTime.UtcNow.AddDays(8)
		);

		// Act
		var result = await _fixture.Handler.HandleAsync("reschedule-flag", newRequest);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
		
		// Verify dates were updated
		var updatedFlag = await _fixture.Repository.GetAsync("reschedule-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.ScheduledEnableDate.Value.ShouldBeInRange(
			newRequest.EnableDate.AddSeconds(-1),
			newRequest.EnableDate.AddSeconds(1));
		updatedFlag.ScheduledDisableDate.Value.ShouldBeInRange(
			newRequest.DisableDate.Value.AddSeconds(-1),
			newRequest.DisableDate.Value.AddSeconds(1));
	}
}

public class ScheduleFlagHandler_DateValidation : IClassFixture<ScheduleFlagHandlerFixture>
{
	private readonly ScheduleFlagHandlerFixture _fixture;

	public ScheduleFlagHandler_DateValidation(ScheduleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_EnableDateInNearFuture_ThenSchedulesSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("near-future-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddMinutes(5),
			DateTime.UtcNow.AddHours(1)
		);

		// Act
		var result = await _fixture.Handler.HandleAsync("near-future-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
	}

	[Fact]
	public async Task If_EnableDateFarInFuture_ThenSchedulesSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("far-future-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(365),
			DateTime.UtcNow.AddDays(400)
		);

		// Act
		var result = await _fixture.Handler.HandleAsync("far-future-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
	}

	[Fact]
	public async Task If_DisableDateBeforeEnableDate_ThenStillSchedules()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("reversed-dates-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(5),
			DateTime.UtcNow.AddDays(2) // Disable before enable
		);

		// Act
		var result = await _fixture.Handler.HandleAsync("reversed-dates-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
		
		// Handler doesn't validate date logic, just stores what's provided
		var updatedFlag = await _fixture.Repository.GetAsync("reversed-dates-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.ScheduledEnableDate.Value.ShouldBeInRange(
			request.EnableDate.AddSeconds(-1),
			request.EnableDate.AddSeconds(1));
		updatedFlag.ScheduledDisableDate.Value.ShouldBeInRange(
			request.DisableDate.Value.AddSeconds(-1),
			request.DisableDate.Value.AddSeconds(1));
	}

	[Fact]
	public async Task If_SameEnableAndDisableDates_ThenSchedules()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("same-dates-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var targetDate = DateTime.UtcNow.AddDays(1);
		var request = new ScheduleFlagRequest(targetDate, targetDate);

		// Act
		var result = await _fixture.Handler.HandleAsync("same-dates-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
	}
}

public class ScheduleFlagHandler_ComplexFlags : IClassFixture<ScheduleFlagHandlerFixture>
{
	private readonly ScheduleFlagHandlerFixture _fixture;

	public ScheduleFlagHandler_ComplexFlags(ScheduleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagWithTargetingRules_ThenPreservesRulesAndSchedules()
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
		await _fixture.Repository.CreateAsync(flag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

		// Act
		var result = await _fixture.Handler.HandleAsync("complex-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
		okResult.Value.TargetingRules.Count.ShouldBe(1); // Preserved
		okResult.Value.EnabledUsers.Count.ShouldBe(2); // Preserved
		okResult.Value.Variations.Count.ShouldBe(2); // Preserved

		// Verify database persistence of all data
		var updatedFlag = await _fixture.Repository.GetAsync("complex-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Scheduled);
		updatedFlag.TargetingRules.Count.ShouldBe(1);
		updatedFlag.EnabledUsers.Count.ShouldBe(2);
	}

	[Fact]
	public async Task If_PermanentFlag_ThenSchedulesSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("permanent-flag", FeatureFlagStatus.Enabled);
		flag.IsPermanent = true;
		await _fixture.Repository.CreateAsync(flag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(30)
		);

		// Act
		var result = await _fixture.Handler.HandleAsync("permanent-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
		okResult.Value.IsPermanent.ShouldBeTrue(); // Preserved

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("permanent-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.IsPermanent.ShouldBeTrue();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Scheduled);
	}

	[Fact]
	public async Task If_FlagWithExistingExpiration_ThenPreservesExpirationAndSchedules()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("expiring-flag", FeatureFlagStatus.Enabled);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(90);
		await _fixture.Repository.CreateAsync(flag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

		// Act
		var result = await _fixture.Handler.HandleAsync("expiring-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Scheduled.ToString());
		okResult.Value.ExpirationDate.ShouldNotBeNull(); // Preserved

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("expiring-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.ExpirationDate.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Scheduled);
	}
}

public class ScheduleFlagHandler_NotFoundScenarios : IClassFixture<ScheduleFlagHandlerFixture>
{
	private readonly ScheduleFlagHandlerFixture _fixture;

	public ScheduleFlagHandler_NotFoundScenarios(ScheduleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await _fixture.ClearAllData();

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

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

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

		// Act
		var result = await _fixture.Handler.HandleAsync(key, request);

		// Assert
		result.ShouldBeOfType<NotFound<string>>();
		var notFoundResult = (NotFound<string>)result;
		notFoundResult.Value.ShouldBe($"Feature flag '{key}' not found");
	}
}

public class ScheduleFlagHandler_CacheInvalidation : IClassFixture<ScheduleFlagHandlerFixture>
{
	private readonly ScheduleFlagHandlerFixture _fixture;

	public ScheduleFlagHandler_CacheInvalidation(ScheduleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagScheduled_ThenInvalidatesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("cached-schedule-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Pre-populate cache
		await _fixture.Cache.SetAsync("cached-schedule-flag", flag);
		var cachedFlag = await _fixture.Cache.GetAsync("cached-schedule-flag");
		cachedFlag.ShouldNotBeNull(); // Verify cache is populated

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

		// Act
		var result = await _fixture.Handler.HandleAsync("cached-schedule-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify cache was cleared
		var cacheAfterSchedule = await _fixture.Cache.GetAsync("cached-schedule-flag");
		cacheAfterSchedule.ShouldBeNull();
	}

	[Fact]
	public async Task If_NonExistentFlagScheduled_ThenNoCacheOperation()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Pre-populate cache with unrelated flag
		var unrelatedFlag = _fixture.CreateTestFlag("unrelated-flag", FeatureFlagStatus.Enabled);
		await _fixture.Cache.SetAsync("unrelated-flag", unrelatedFlag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

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

public class ScheduleFlagHandler_AuditLogging : IClassFixture<ScheduleFlagHandlerFixture>
{
	private readonly ScheduleFlagHandlerFixture _fixture;

	public ScheduleFlagHandler_AuditLogging(ScheduleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_SuccessfulScheduling_ThenLogsWithCorrectDetails()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("audit-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

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
					v.ToString()!.Contains("Feature flag audit-flag scheduled by test-user for")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task If_MultipleSchedulings_ThenLogsEachOperation()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag1 = _fixture.CreateTestFlag("schedule-flag-1", FeatureFlagStatus.Disabled);
		var flag2 = _fixture.CreateTestFlag("schedule-flag-2", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

		// Act
		await _fixture.Handler.HandleAsync("schedule-flag-1", request);
		await _fixture.Handler.HandleAsync("schedule-flag-2", request);

		// Assert
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("schedule-flag-1")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("schedule-flag-2")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class ScheduleFlagHandler_ErrorScenarios : IClassFixture<ScheduleFlagHandlerFixture>
{
	private readonly ScheduleFlagHandlerFixture _fixture;

	public ScheduleFlagHandler_ErrorScenarios(ScheduleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_RepositoryThrowsException_ThenReturnsInternalServerError()
	{
		// Arrange
		await _fixture.ClearAllData();
		await _fixture.DisposeContainers();

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

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
		var badHandler = new ScheduleFlagHandler(badRepository, _fixture.Cache, _fixture.MockLogger.Object, _fixture.CurrentUserService);

		var request = new ScheduleFlagRequest(
			DateTime.UtcNow.AddDays(1),
			DateTime.UtcNow.AddDays(7)
		);

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
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error scheduling feature flag bad-connection-flag")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class ScheduleFlagHandlerFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public ScheduleFlagHandler Handler { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public CurrentUserService CurrentUserService { get; private set; } = null!;
	public Mock<ILogger<ScheduleFlagHandler>> MockLogger { get; }
	public Mock<ILogger<PostgreSQLFeatureFlagRepository>> MockRepositoryLogger { get; }
	
	private IConnectionMultiplexer _redisConnection = null!;

	public ScheduleFlagHandlerFixture()
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

		MockLogger = new Mock<ILogger<ScheduleFlagHandler>>();
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
		Handler = new ScheduleFlagHandler(Repository, Cache, MockLogger.Object, CurrentUserService);
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