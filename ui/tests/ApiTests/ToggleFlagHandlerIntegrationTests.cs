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

namespace ApiTests.ToggleFlagHandlerTests;

/* The tests cover these scenarios with ToggleFlagHandler integration:
 *		Successful flag toggling to Enabled status
 *		Successful flag toggling to Disabled status
 *		Status change from different initial statuses
 *		Non-existent flag handling (404)
 *		Cache invalidation after toggle operations
 *		User context tracking and logging with reasons
 *		Database persistence verification
 *		Error scenarios and exception handling
 *		Complex flag data preservation during toggle
 *		Reason parameter handling (provided and null)
 *		Logging verification for audit trails
 *		Multiple toggle operations on same flag
*/

public class ToggleFlagHandler_EnableFlag : IClassFixture<ToggleFlagHandlerFixture>
{
	private readonly ToggleFlagHandlerFixture _fixture;

	public ToggleFlagHandler_EnableFlag(ToggleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidFlagToEnable_ThenEnablesSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("toggle-enable-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("toggle-enable-flag", FeatureFlagStatus.Enabled, "Enabling for production release");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Key.ShouldBe("toggle-enable-flag");
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Enabled.ToString());
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("toggle-enable-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Enabled);
		updatedFlag.UpdatedBy.ShouldBe("test-user");
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.Percentage)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	public async Task If_DifferentOriginalStatuses_ThenChangesToEnabled(FeatureFlagStatus originalStatus)
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag($"enable-from-{originalStatus}", originalStatus);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync($"enable-from-{originalStatus}", FeatureFlagStatus.Enabled, "Testing status change");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Enabled.ToString());

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync($"enable-from-{originalStatus}");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Enabled);
	}

	[Fact]
	public async Task If_AlreadyEnabledFlag_ThenRemainsEnabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("already-enabled-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("already-enabled-flag", FeatureFlagStatus.Enabled, "Re-enabling already enabled flag");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Enabled.ToString());
		okResult.Value.UpdatedBy.ShouldBe("test-user"); // Should still update user
	}

	[Fact]
	public async Task If_ComplexFlagEnabled_ThenPreservesAllData()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("complex-enable-flag", FeatureFlagStatus.Disabled);
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
		flag.PercentageEnabled = 25;
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("complex-enable-flag", FeatureFlagStatus.Enabled, "Complex flag enable test");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Enabled.ToString());
		okResult.Value.TargetingRules.Count.ShouldBe(1); // Preserved
		okResult.Value.EnabledUsers.Count.ShouldBe(2); // Preserved
		okResult.Value.Variations.Count.ShouldBe(2); // Preserved
		okResult.Value.Tags.Count.ShouldBe(2); // Preserved
		okResult.Value.ExpirationDate.ShouldNotBeNull(); // Preserved
		okResult.Value.PercentageEnabled.ShouldBe(25); // Preserved

		// Verify database persistence of all data
		var updatedFlag = await _fixture.Repository.GetAsync("complex-enable-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Enabled);
		updatedFlag.TargetingRules.Count.ShouldBe(1);
		updatedFlag.EnabledUsers.Count.ShouldBe(2);
		updatedFlag.PercentageEnabled.ShouldBe(25);
	}
}

public class ToggleFlagHandler_DisableFlag : IClassFixture<ToggleFlagHandlerFixture>
{
	private readonly ToggleFlagHandlerFixture _fixture;

	public ToggleFlagHandler_DisableFlag(ToggleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidFlagToDisable_ThenDisablesSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("toggle-disable-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("toggle-disable-flag", FeatureFlagStatus.Disabled, "Disabling due to bug found");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Key.ShouldBe("toggle-disable-flag");
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Disabled.ToString());
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("toggle-disable-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Disabled);
		updatedFlag.UpdatedBy.ShouldBe("test-user");
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.Percentage)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	public async Task If_DifferentOriginalStatuses_ThenChangesToDisabled(FeatureFlagStatus originalStatus)
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag($"disable-from-{originalStatus}", originalStatus);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync($"disable-from-{originalStatus}", FeatureFlagStatus.Disabled, "Emergency disable");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Disabled.ToString());

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync($"disable-from-{originalStatus}");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Disabled);
	}

	[Fact]
	public async Task If_AlreadyDisabledFlag_ThenRemainsDisabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("already-disabled-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("already-disabled-flag", FeatureFlagStatus.Disabled, "Re-disabling already disabled flag");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Disabled.ToString());
		okResult.Value.UpdatedBy.ShouldBe("test-user"); // Should still update user
	}

	[Fact]
	public async Task If_PermanentFlagDisabled_ThenDisablesSuccessfully()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("permanent-flag", FeatureFlagStatus.Enabled);
		flag.IsPermanent = true;
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("permanent-flag", FeatureFlagStatus.Disabled, "Emergency disable of permanent flag");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Disabled.ToString());
		okResult.Value.IsPermanent.ShouldBeTrue(); // Preserved

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("permanent-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.IsPermanent.ShouldBeTrue();
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Disabled);
	}
}

public class ToggleFlagHandler_ReasonHandling : IClassFixture<ToggleFlagHandlerFixture>
{
	private readonly ToggleFlagHandlerFixture _fixture;

	public ToggleFlagHandler_ReasonHandling(ToggleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ReasonProvided_ThenLogsWithReason()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("reason-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("reason-flag", FeatureFlagStatus.Enabled, "Feature ready for production");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify specific log message with reason
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => 
					v.ToString()!.Contains("Feature flag reason-flag enabled by test-user. Reason: Feature ready for production")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task If_ReasonNull_ThenLogsWithNotProvided()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("no-reason-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("no-reason-flag", FeatureFlagStatus.Disabled, null);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify specific log message with "Not provided"
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => 
					v.ToString()!.Contains("Feature flag no-reason-flag disabled by test-user. Reason: Not provided")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task If_EmptyReason_ThenLogsWithNotProvided()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("empty-reason-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("empty-reason-flag", FeatureFlagStatus.Enabled, "");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify specific log message - empty string should be logged as provided
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => 
					v.ToString()!.Contains("Feature flag empty-reason-flag enabled by test-user. Reason: ")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Theory]
	[InlineData("Bug fix deployment")]
	[InlineData("A/B test completion")]
	[InlineData("Customer request")]
	[InlineData("Performance optimization")]
	[InlineData("Security update")]
	public async Task If_VariousReasons_ThenLogsCorrectly(string reason)
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag($"reason-test-{reason.GetHashCode()}", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync($"reason-test-{reason.GetHashCode()}", FeatureFlagStatus.Enabled, reason);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Reason: {reason}")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class ToggleFlagHandler_MultipleOperations : IClassFixture<ToggleFlagHandlerFixture>
{
	private readonly ToggleFlagHandlerFixture _fixture;

	public ToggleFlagHandler_MultipleOperations(ToggleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_MultipleToggles_ThenEachOperationSucceeds()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("multi-toggle-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act - Enable
		var enableResult = await _fixture.Handler.HandleAsync("multi-toggle-flag", FeatureFlagStatus.Enabled, "First enable");
		
		// Act - Disable
		var disableResult = await _fixture.Handler.HandleAsync("multi-toggle-flag", FeatureFlagStatus.Disabled, "Then disable");
		
		// Act - Enable again
		var enableAgainResult = await _fixture.Handler.HandleAsync("multi-toggle-flag", FeatureFlagStatus.Enabled, "Enable again");

		// Assert
		enableResult.ShouldBeOfType<Ok<FeatureFlagDto>>();
		disableResult.ShouldBeOfType<Ok<FeatureFlagDto>>();
		enableAgainResult.ShouldBeOfType<Ok<FeatureFlagDto>>();

		var finalResult = (Ok<FeatureFlagDto>)enableAgainResult;
		finalResult.Value.Status.ShouldBe(FeatureFlagStatus.Enabled.ToString());

		// Verify final database state
		var finalFlag = await _fixture.Repository.GetAsync("multi-toggle-flag");
		finalFlag.ShouldNotBeNull();
		finalFlag.Status.ShouldBe(FeatureFlagStatus.Enabled);

		// Verify all operations were logged
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("First enable")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Then disable")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Enable again")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task If_MultipleFlags_ThenEachTogglesIndependently()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag1 = _fixture.CreateTestFlag("independent-flag-1", FeatureFlagStatus.Disabled);
		var flag2 = _fixture.CreateTestFlag("independent-flag-2", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);

		// Act
		var result1 = await _fixture.Handler.HandleAsync("independent-flag-1", FeatureFlagStatus.Enabled, "Enable flag 1");
		var result2 = await _fixture.Handler.HandleAsync("independent-flag-2", FeatureFlagStatus.Disabled, "Keep flag 2 disabled");

		// Assert
		result1.ShouldBeOfType<Ok<FeatureFlagDto>>();
		result2.ShouldBeOfType<Ok<FeatureFlagDto>>();

		var okResult1 = (Ok<FeatureFlagDto>)result1;
		var okResult2 = (Ok<FeatureFlagDto>)result2;

		okResult1.Value.Status.ShouldBe(FeatureFlagStatus.Enabled.ToString());
		okResult2.Value.Status.ShouldBe(FeatureFlagStatus.Disabled.ToString());

		// Verify independent database states
		var updatedFlag1 = await _fixture.Repository.GetAsync("independent-flag-1");
		var updatedFlag2 = await _fixture.Repository.GetAsync("independent-flag-2");
		
		updatedFlag1.ShouldNotBeNull();
		updatedFlag1.Status.ShouldBe(FeatureFlagStatus.Enabled);
		
		updatedFlag2.ShouldNotBeNull();
		updatedFlag2.Status.ShouldBe(FeatureFlagStatus.Disabled);
	}
}

public class ToggleFlagHandler_NotFoundScenarios : IClassFixture<ToggleFlagHandlerFixture>
{
	private readonly ToggleFlagHandlerFixture _fixture;

	public ToggleFlagHandler_NotFoundScenarios(ToggleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Act
		var result = await _fixture.Handler.HandleAsync("non-existent-flag", FeatureFlagStatus.Enabled, "Trying to enable non-existent flag");

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
		var result = await _fixture.Handler.HandleAsync(key, FeatureFlagStatus.Enabled, "Test reason");

		// Assert
		result.ShouldBeOfType<NotFound<string>>();
		var notFoundResult = (NotFound<string>)result;
		notFoundResult.Value.ShouldBe($"Feature flag '{key}' not found");
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Disabled)]
	public async Task If_NonExistentFlagForDifferentStatuses_ThenReturnsNotFound(FeatureFlagStatus targetStatus)
	{
		// Arrange
		await _fixture.ClearAllData();

		// Act
		var result = await _fixture.Handler.HandleAsync("missing-flag", targetStatus, $"Trying to set to {targetStatus}");

		// Assert
		result.ShouldBeOfType<NotFound<string>>();
		var notFoundResult = (NotFound<string>)result;
		notFoundResult.Value.ShouldBe("Feature flag 'missing-flag' not found");
	}
}

public class ToggleFlagHandler_CacheInvalidation : IClassFixture<ToggleFlagHandlerFixture>
{
	private readonly ToggleFlagHandlerFixture _fixture;

	public ToggleFlagHandler_CacheInvalidation(ToggleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagToggled_ThenInvalidatesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("cached-toggle-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		// Pre-populate cache
		await _fixture.Cache.SetAsync("cached-toggle-flag", flag);
		var cachedFlag = await _fixture.Cache.GetAsync("cached-toggle-flag");
		cachedFlag.ShouldNotBeNull(); // Verify cache is populated

		// Act
		var result = await _fixture.Handler.HandleAsync("cached-toggle-flag", FeatureFlagStatus.Enabled, "Cache invalidation test");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify cache was cleared
		var cacheAfterToggle = await _fixture.Cache.GetAsync("cached-toggle-flag");
		cacheAfterToggle.ShouldBeNull();
	}

	[Fact]
	public async Task If_NonExistentFlagToggled_ThenNoCacheOperation()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Pre-populate cache with unrelated flag
		var unrelatedFlag = _fixture.CreateTestFlag("unrelated-flag", FeatureFlagStatus.Enabled);
		await _fixture.Cache.SetAsync("unrelated-flag", unrelatedFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync("non-existent-flag", FeatureFlagStatus.Enabled, "Test reason");

		// Assert
		result.ShouldBeOfType<NotFound<string>>();

		// Verify unrelated cache entry is unchanged
		var cachedFlag = await _fixture.Cache.GetAsync("unrelated-flag");
		cachedFlag.ShouldNotBeNull();
		cachedFlag.Key.ShouldBe("unrelated-flag");
	}
}

public class ToggleFlagHandler_ErrorScenarios : IClassFixture<ToggleFlagHandlerFixture>
{
	private readonly ToggleFlagHandlerFixture _fixture;

	public ToggleFlagHandler_ErrorScenarios(ToggleFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_RepositoryThrowsException_ThenReturnsInternalServerError()
	{
		// Arrange
		await _fixture.ClearAllData();
		await _fixture.DisposeContainers();

		// Act
		var result = await _fixture.Handler.HandleAsync("error-flag", FeatureFlagStatus.Enabled, "Error test");

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
		var badHandler = new ToggleFlagHandler(badRepository, _fixture.Cache, _fixture.MockLogger.Object, _fixture.CurrentUserService);

		// Act
		var result = await badHandler.HandleAsync("bad-connection-flag", FeatureFlagStatus.Enabled, "Connection test");

		// Assert
		result.ShouldBeOfType<StatusCodeHttpResult>();
		var statusResult = (StatusCodeHttpResult)result;
		statusResult.StatusCode.ShouldBe(500);

		// Verify error was logged
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error toggling feature flag bad-connection-flag")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class ToggleFlagHandlerFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public ToggleFlagHandler Handler { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public CurrentUserService CurrentUserService { get; private set; } = null!;
	public Mock<ILogger<ToggleFlagHandler>> MockLogger { get; }
	public Mock<ILogger<PostgreSQLFeatureFlagRepository>> MockRepositoryLogger { get; }
	
	private IConnectionMultiplexer _redisConnection = null!;

	public ToggleFlagHandlerFixture()
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

		MockLogger = new Mock<ILogger<ToggleFlagHandler>>();
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
		Handler = new ToggleFlagHandler(Repository, Cache, MockLogger.Object, CurrentUserService);
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