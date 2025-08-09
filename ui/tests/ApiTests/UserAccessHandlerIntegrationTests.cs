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

namespace ApiTests.UserAccessHandlerTests;

/* The tests cover these scenarios with UserAccessHandler integration:
 *		Successful user enabling with list management
 *		Successful user disabling with list management
 *		User switching between enabled/disabled states
 *		Multiple users management in single operation
 *		Non-existent flag handling (404)
 *		Empty and null user lists handling
 *		Duplicate user handling in lists
 *		Cache invalidation after user access changes
 *		User context tracking and logging
 *		Database persistence verification
 *		Error scenarios and exception handling
 *		Complex flag data preservation during user access changes
 *		Audit logging for user access modifications
*/

public class UserAccessHandler_EnableUsers : IClassFixture<UserAccessHandlerFixture>
{
	private readonly UserAccessHandlerFixture _fixture;

	public UserAccessHandler_EnableUsers(UserAccessHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidUsersToEnable_ThenAddsToEnabledList()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("user-enable-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "user1", "user2", "user3" };

		// Act
		var result = await _fixture.Handler.HandleAsync("user-enable-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Key.ShouldBe("user-enable-flag");
		okResult.Value.EnabledUsers.Count.ShouldBe(3);
		okResult.Value.EnabledUsers.ShouldContain("user1");
		okResult.Value.EnabledUsers.ShouldContain("user2");
		okResult.Value.EnabledUsers.ShouldContain("user3");
		okResult.Value.DisabledUsers.Count.ShouldBe(0);
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("user-enable-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.EnabledUsers.Count.ShouldBe(3);
		updatedFlag.EnabledUsers.ShouldContain("user1");
		updatedFlag.UpdatedBy.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_UserAlreadyEnabled_ThenSkipsDuplicate()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("duplicate-enable-flag", FeatureFlagStatus.UserTargeted);
		flag.EnabledUsers = new List<string> { "existing-user" };
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "existing-user", "new-user" };

		// Act
		var result = await _fixture.Handler.HandleAsync("duplicate-enable-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.EnabledUsers.Count.ShouldBe(2);
		okResult.Value.EnabledUsers.ShouldContain("existing-user");
		okResult.Value.EnabledUsers.ShouldContain("new-user");
		
		// Should not have duplicates
		okResult.Value.EnabledUsers.Count(u => u == "existing-user").ShouldBe(1);
	}

	[Fact]
	public async Task If_UserInDisabledList_ThenMovesToEnabledList()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("move-user-flag", FeatureFlagStatus.UserTargeted);
		flag.DisabledUsers = new List<string> { "disabled-user", "other-disabled" };
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "disabled-user" };

		// Act
		var result = await _fixture.Handler.HandleAsync("move-user-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.EnabledUsers.Count.ShouldBe(1);
		okResult.Value.EnabledUsers.ShouldContain("disabled-user");
		okResult.Value.DisabledUsers.Count.ShouldBe(1);
		okResult.Value.DisabledUsers.ShouldContain("other-disabled");
		okResult.Value.DisabledUsers.ShouldNotContain("disabled-user");

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("move-user-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.EnabledUsers.ShouldContain("disabled-user");
		updatedFlag.DisabledUsers.ShouldNotContain("disabled-user");
	}

	[Fact]
	public async Task If_SingleUser_ThenEnablesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("single-user-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "single-user@example.com" };

		// Act
		var result = await _fixture.Handler.HandleAsync("single-user-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.EnabledUsers.Count.ShouldBe(1);
		okResult.Value.EnabledUsers[0].ShouldBe("single-user@example.com");
	}

	[Fact]
	public async Task If_ManyUsers_ThenEnablesAllCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("many-users-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string>();
		for (int i = 1; i <= 50; i++)
		{
			userIds.Add($"user{i}@example.com");
		}

		// Act
		var result = await _fixture.Handler.HandleAsync("many-users-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.EnabledUsers.Count.ShouldBe(50);
		okResult.Value.EnabledUsers.ShouldContain("user1@example.com");
		okResult.Value.EnabledUsers.ShouldContain("user50@example.com");
	}
}

public class UserAccessHandler_DisableUsers : IClassFixture<UserAccessHandlerFixture>
{
	private readonly UserAccessHandlerFixture _fixture;

	public UserAccessHandler_DisableUsers(UserAccessHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidUsersToDisable_ThenAddsToDisabledList()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("user-disable-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "user1", "user2", "user3" };

		// Act
		var result = await _fixture.Handler.HandleAsync("user-disable-flag", userIds, false);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Key.ShouldBe("user-disable-flag");
		okResult.Value.DisabledUsers.Count.ShouldBe(3);
		okResult.Value.DisabledUsers.ShouldContain("user1");
		okResult.Value.DisabledUsers.ShouldContain("user2");
		okResult.Value.DisabledUsers.ShouldContain("user3");
		okResult.Value.EnabledUsers.Count.ShouldBe(0);
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("user-disable-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.DisabledUsers.Count.ShouldBe(3);
		updatedFlag.DisabledUsers.ShouldContain("user1");
		updatedFlag.UpdatedBy.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_UserAlreadyDisabled_ThenSkipsDuplicate()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("duplicate-disable-flag", FeatureFlagStatus.UserTargeted);
		flag.DisabledUsers = new List<string> { "existing-disabled-user" };
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "existing-disabled-user", "new-disabled-user" };

		// Act
		var result = await _fixture.Handler.HandleAsync("duplicate-disable-flag", userIds, false);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.DisabledUsers.Count.ShouldBe(2);
		okResult.Value.DisabledUsers.ShouldContain("existing-disabled-user");
		okResult.Value.DisabledUsers.ShouldContain("new-disabled-user");
		
		// Should not have duplicates
		okResult.Value.DisabledUsers.Count(u => u == "existing-disabled-user").ShouldBe(1);
	}

	[Fact]
	public async Task If_UserInEnabledList_ThenMovesToDisabledList()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("move-to-disabled-flag", FeatureFlagStatus.UserTargeted);
		flag.EnabledUsers = new List<string> { "enabled-user", "other-enabled" };
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "enabled-user" };

		// Act
		var result = await _fixture.Handler.HandleAsync("move-to-disabled-flag", userIds, false);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.DisabledUsers.Count.ShouldBe(1);
		okResult.Value.DisabledUsers.ShouldContain("enabled-user");
		okResult.Value.EnabledUsers.Count.ShouldBe(1);
		okResult.Value.EnabledUsers.ShouldContain("other-enabled");
		okResult.Value.EnabledUsers.ShouldNotContain("enabled-user");

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("move-to-disabled-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.DisabledUsers.ShouldContain("enabled-user");
		updatedFlag.EnabledUsers.ShouldNotContain("enabled-user");
	}
}

public class UserAccessHandler_ComplexScenarios : IClassFixture<UserAccessHandlerFixture>
{
	private readonly UserAccessHandlerFixture _fixture;

	public UserAccessHandler_ComplexScenarios(UserAccessHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_MixedUserOperations_ThenHandlesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("mixed-operations-flag", FeatureFlagStatus.UserTargeted);
		flag.EnabledUsers = new List<string> { "already-enabled" };
		flag.DisabledUsers = new List<string> { "to-be-enabled" };
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "already-enabled", "to-be-enabled", "new-user" };

		// Act
		var result = await _fixture.Handler.HandleAsync("mixed-operations-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.EnabledUsers.Count.ShouldBe(3);
		okResult.Value.EnabledUsers.ShouldContain("already-enabled");
		okResult.Value.EnabledUsers.ShouldContain("to-be-enabled");
		okResult.Value.EnabledUsers.ShouldContain("new-user");
		okResult.Value.DisabledUsers.Count.ShouldBe(0);
	}

	[Fact]
	public async Task If_FlagWithComplexData_ThenPreservesAllData()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("complex-user-flag", FeatureFlagStatus.UserTargeted);
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
		flag.PercentageEnabled = 25;
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "complex-user1", "complex-user2" };

		// Act
		var result = await _fixture.Handler.HandleAsync("complex-user-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.EnabledUsers.Count.ShouldBe(2);
		okResult.Value.TargetingRules.Count.ShouldBe(1); // Preserved
		okResult.Value.Variations.Count.ShouldBe(2); // Preserved
		okResult.Value.Tags.Count.ShouldBe(2); // Preserved
		okResult.Value.ExpirationDate.ShouldNotBeNull(); // Preserved
		okResult.Value.PercentageEnabled.ShouldBe(25); // Preserved

		// Verify database persistence of all data
		var updatedFlag = await _fixture.Repository.GetAsync("complex-user-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.EnabledUsers.Count.ShouldBe(2);
		updatedFlag.TargetingRules.Count.ShouldBe(1);
		updatedFlag.PercentageEnabled.ShouldBe(25);
		updatedFlag.ExpirationDate.ShouldNotBeNull();
	}

	[Theory]
	[InlineData("user@example.com")]
	[InlineData("user_with_underscores")]
	[InlineData("user-with-dashes")]
	[InlineData("123-numeric-user")]
	[InlineData("user.with.dots")]
	public async Task If_DifferentUserIdFormats_ThenHandlesCorrectly(string userId)
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("format-test-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { userId };

		// Act
		var result = await _fixture.Handler.HandleAsync("format-test-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.EnabledUsers.Count.ShouldBe(1);
		okResult.Value.EnabledUsers[0].ShouldBe(userId);
	}

	[Fact]
	public async Task If_EmptyUserIdInList_ThenHandlesGracefully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("empty-user-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "valid-user", "", "another-valid-user" };

		// Act
		var result = await _fixture.Handler.HandleAsync("empty-user-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.EnabledUsers.Count.ShouldBe(3);
		okResult.Value.EnabledUsers.ShouldContain("valid-user");
		okResult.Value.EnabledUsers.ShouldContain("");
		okResult.Value.EnabledUsers.ShouldContain("another-valid-user");
	}
}

public class UserAccessHandler_NotFoundScenarios : IClassFixture<UserAccessHandlerFixture>
{
	private readonly UserAccessHandlerFixture _fixture;

	public UserAccessHandler_NotFoundScenarios(UserAccessHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await _fixture.ClearAllData();

		var userIds = new List<string> { "user1", "user2" };

		// Act
		var result = await _fixture.Handler.HandleAsync("non-existent-flag", userIds, true);

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

		var userIds = new List<string> { "user1" };

		// Act
		var result = await _fixture.Handler.HandleAsync(key, userIds, true);

		// Assert
		result.ShouldBeOfType<NotFound<string>>();
		var notFoundResult = (NotFound<string>)result;
		notFoundResult.Value.ShouldBe($"Feature flag '{key}' not found");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task If_NonExistentFlagForDifferentOperations_ThenReturnsNotFound(bool enable)
	{
		// Arrange
		await _fixture.ClearAllData();

		var userIds = new List<string> { "user1" };

		// Act
		var result = await _fixture.Handler.HandleAsync("missing-flag", userIds, enable);

		// Assert
		result.ShouldBeOfType<NotFound<string>>();
		var notFoundResult = (NotFound<string>)result;
		notFoundResult.Value.ShouldBe("Feature flag 'missing-flag' not found");
	}
}

public class UserAccessHandler_CacheInvalidation : IClassFixture<UserAccessHandlerFixture>
{
	private readonly UserAccessHandlerFixture _fixture;

	public UserAccessHandler_CacheInvalidation(UserAccessHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_UserAccessModified_ThenInvalidatesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("cached-user-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		// Pre-populate cache
		await _fixture.Cache.SetAsync("cached-user-flag", flag);
		var cachedFlag = await _fixture.Cache.GetAsync("cached-user-flag");
		cachedFlag.ShouldNotBeNull(); // Verify cache is populated

		var userIds = new List<string> { "cache-test-user" };

		// Act
		var result = await _fixture.Handler.HandleAsync("cached-user-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify cache was cleared
		var cacheAfterUpdate = await _fixture.Cache.GetAsync("cached-user-flag");
		cacheAfterUpdate.ShouldBeNull();
	}

	[Fact]
	public async Task If_NonExistentFlagUserAccess_ThenNoCacheOperation()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Pre-populate cache with unrelated flag
		var unrelatedFlag = _fixture.CreateTestFlag("unrelated-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Cache.SetAsync("unrelated-flag", unrelatedFlag);

		var userIds = new List<string> { "user1" };

		// Act
		var result = await _fixture.Handler.HandleAsync("non-existent-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<NotFound<string>>();

		// Verify unrelated cache entry is unchanged
		var cachedFlag = await _fixture.Cache.GetAsync("unrelated-flag");
		cachedFlag.ShouldNotBeNull();
		cachedFlag.Key.ShouldBe("unrelated-flag");
	}
}

public class UserAccessHandler_AuditLogging : IClassFixture<UserAccessHandlerFixture>
{
	private readonly UserAccessHandlerFixture _fixture;

	public UserAccessHandler_AuditLogging(UserAccessHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_UsersEnabled_ThenLogsWithCorrectDetails()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("audit-enable-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "audit-user1", "audit-user2" };

		// Act
		var result = await _fixture.Handler.HandleAsync("audit-enable-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify specific log message structure
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => 
					v.ToString()!.Contains("Feature flag audit-enable-flag enabled for users audit-user1, audit-user2 by test-user")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task If_UsersDisabled_ThenLogsWithCorrectDetails()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("audit-disable-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "disable-user1", "disable-user2", "disable-user3" };

		// Act
		var result = await _fixture.Handler.HandleAsync("audit-disable-flag", userIds, false);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify specific log message structure
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => 
					v.ToString()!.Contains("Feature flag audit-disable-flag disabled for users disable-user1, disable-user2, disable-user3 by test-user")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task If_SingleUserOperation_ThenLogsCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("single-audit-flag", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "single-audit-user@example.com" };

		// Act
		var result = await _fixture.Handler.HandleAsync("single-audit-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("single-audit-user@example.com")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class UserAccessHandler_ErrorScenarios : IClassFixture<UserAccessHandlerFixture>
{
	private readonly UserAccessHandlerFixture _fixture;

	public UserAccessHandler_ErrorScenarios(UserAccessHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_RepositoryThrowsException_ThenReturnsInternalServerError()
	{
		// Arrange
		await _fixture.ClearAllData();
		await _fixture.DisposeContainers();

		var userIds = new List<string> { "error-user" };

		// Act
		var result = await _fixture.Handler.HandleAsync("error-flag", userIds, true);

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
		var badHandler = new UserAccessHandler(badRepository, _fixture.Cache, _fixture.MockLogger.Object, _fixture.CurrentUserService);

		var userIds = new List<string> { "connection-test-user" };

		// Act
		var result = await badHandler.HandleAsync("bad-connection-flag", userIds, true);

		// Assert
		result.ShouldBeOfType<StatusCodeHttpResult>();
		var statusResult = (StatusCodeHttpResult)result;
		statusResult.StatusCode.ShouldBe(500);

		// Verify error was logged
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error managing user access for feature flag bad-connection-flag")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class UserAccessHandlerFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public UserAccessHandler Handler { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public CurrentUserService CurrentUserService { get; private set; } = null!;
	public Mock<ILogger<UserAccessHandler>> MockLogger { get; }
	public Mock<ILogger<PostgreSQLFeatureFlagRepository>> MockRepositoryLogger { get; }
	
	private IConnectionMultiplexer _redisConnection = null!;

	public UserAccessHandlerFixture()
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

		MockLogger = new Mock<ILogger<UserAccessHandler>>();
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
		Handler = new UserAccessHandler(Repository, Cache, MockLogger.Object, CurrentUserService);
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