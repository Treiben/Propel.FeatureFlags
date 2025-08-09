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

namespace ApiTests.ModifyFlagHandlerTests;

/* The tests cover these scenarios with ModifyFlagHandler integration:
 *		Successful flag modification with partial updates
 *		Field-specific updates (name, description, status, etc.)
 *		Time window updates with TimeOnly to TimeSpan conversion
 *		Targeting rules and user list modifications
 *		Variations and tags updates
 *		Complex nested object updates
 *		Non-existent flag handling (404)
 *		Cache invalidation after updates
 *		User context tracking and logging
 *		Error scenarios and exception handling
 *		Null field handling and selective updates
 *		Database persistence verification
*/

public class ModifyFlagHandler_SuccessfulModification : IClassFixture<ModifyFlagHandlerFixture>
{
	private readonly ModifyFlagHandlerFixture _fixture;

	public ModifyFlagHandler_SuccessfulModification(ModifyFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ValidRequest_ThenModifiesFlag()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("modify-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			Name = "Updated Flag Name",
			Description = "Updated description",
			Status = FeatureFlagStatus.Enabled
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("modify-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Key.ShouldBe("modify-flag");
		okResult.Value.Name.ShouldBe("Updated Flag Name");
		okResult.Value.Description.ShouldBe("Updated description");
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Enabled.ToString());
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify database persistence
		var updatedFlag = await _fixture.Repository.GetAsync("modify-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Name.ShouldBe("Updated Flag Name");
		updatedFlag.Status.ShouldBe(FeatureFlagStatus.Enabled);
	}

	[Fact]
	public async Task If_PartialUpdate_ThenModifiesOnlySpecifiedFields()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("partial-flag", FeatureFlagStatus.Enabled);
		existingFlag.Name = "Original Name";
		existingFlag.Description = "Original Description";
		existingFlag.PercentageEnabled = 25;
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			Name = "Updated Name Only"
			// Other fields are null/not specified
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("partial-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Name.ShouldBe("Updated Name Only");
		okResult.Value.Description.ShouldBe("Original Description"); // Unchanged
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Enabled.ToString()); // Unchanged
		okResult.Value.PercentageEnabled.ShouldBe(25); // Unchanged
	}

	[Fact]
	public async Task If_AllFieldsUpdated_ThenModifiesCompleteFlag()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("complete-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			Name = "Complete Update",
			Description = "Fully updated description",
			Status = FeatureFlagStatus.Percentage,
			ExpirationDate = DateTime.UtcNow.AddDays(30),
			ScheduledEnableDate = DateTime.UtcNow.AddDays(1),
			ScheduledDisableDate = DateTime.UtcNow.AddDays(7),
			WindowStartTime = new TimeOnly(9, 0),
			WindowEndTime = new TimeOnly(17, 30),
			TimeZone = "America/New_York",
			WindowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday },
			PercentageEnabled = 75,
			IsPermanent = true
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("complete-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Name.ShouldBe("Complete Update");
		okResult.Value.Description.ShouldBe("Fully updated description");
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Percentage.ToString());
		okResult.Value.PercentageEnabled.ShouldBe(75);
		okResult.Value.IsPermanent.ShouldBeTrue();
		okResult.Value.WindowStartTime.ShouldBe(new TimeOnly(9, 0));
		okResult.Value.WindowEndTime.ShouldBe(new TimeOnly(17, 30));
		okResult.Value.TimeZone.ShouldBe("America/New_York");
		okResult.Value.WindowDays.ShouldContain(DayOfWeek.Monday);
		okResult.Value.WindowDays.ShouldContain(DayOfWeek.Friday);
	}
}

public class ModifyFlagHandler_TimeWindowUpdates : IClassFixture<ModifyFlagHandlerFixture>
{
	private readonly ModifyFlagHandlerFixture _fixture;

	public ModifyFlagHandler_TimeWindowUpdates(ModifyFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_TimeWindowUpdated_ThenConvertsTimeOnlyToTimeSpan()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("time-flag", FeatureFlagStatus.TimeWindow);
		existingFlag.WindowStartTime = TimeSpan.FromHours(8);
		existingFlag.WindowEndTime = TimeSpan.FromHours(16);
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			WindowStartTime = new TimeOnly(10, 30),
			WindowEndTime = new TimeOnly(18, 45),
			TimeZone = "Pacific/Honolulu",
			WindowDays = new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday }
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("time-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.WindowStartTime.ShouldBe(new TimeOnly(10, 30));
		okResult.Value.WindowEndTime.ShouldBe(new TimeOnly(18, 45));
		okResult.Value.TimeZone.ShouldBe("Pacific/Honolulu");
		okResult.Value.WindowDays.Count.ShouldBe(3);

		// Verify database conversion
		var updatedFlag = await _fixture.Repository.GetAsync("time-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.WindowStartTime.ShouldBe(new TimeSpan(10, 30, 0));
		updatedFlag.WindowEndTime.ShouldBe(new TimeSpan(18, 45, 0));
	}

	[Fact]
	public async Task If_OnlyTimeZoneUpdated_ThenKeepsExistingTimes()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("timezone-flag", FeatureFlagStatus.TimeWindow);
		existingFlag.WindowStartTime = TimeSpan.FromHours(9);
		existingFlag.WindowEndTime = TimeSpan.FromHours(17);
		existingFlag.TimeZone = "UTC";
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			TimeZone = "Europe/London"
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("timezone-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.TimeZone.ShouldBe("Europe/London");
		okResult.Value.WindowStartTime.ShouldBe(new TimeOnly(9, 0)); // Unchanged
		okResult.Value.WindowEndTime.ShouldBe(new TimeOnly(17, 0)); // Unchanged
	}
}

public class ModifyFlagHandler_TargetingAndUsers : IClassFixture<ModifyFlagHandlerFixture>
{
	private readonly ModifyFlagHandlerFixture _fixture;

	public ModifyFlagHandler_TargetingAndUsers(ModifyFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_TargetingRulesUpdated_ThenReplacesRules()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("targeting-flag", FeatureFlagStatus.UserTargeted);
		existingFlag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule { Attribute = "old", Operator = TargetingOperator.In, Values = new List<string> { "value" } }
		};
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			TargetingRules = new List<TargetingRule>
			{
				new TargetingRule
				{
					Attribute = "country",
					Operator = TargetingOperator.In,
					Values = new List<string> { "US", "CA", "MX" },
					Variation = "north-america"
				},
				new TargetingRule
				{
					Attribute = "userTier",
					Operator = TargetingOperator.In,
					Values = new List<string> { "premium", "enterprise" },
					Variation = "premium-features"
				}
			}
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("targeting-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.TargetingRules.Count.ShouldBe(2);
		okResult.Value.TargetingRules[0].Attribute.ShouldBe("country");
		okResult.Value.TargetingRules[0].Values.ShouldContain("US");
		okResult.Value.TargetingRules[1].Variation.ShouldBe("premium-features");
	}

	[Fact]
	public async Task If_UserListsUpdated_ThenReplacesLists()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("users-flag", FeatureFlagStatus.Enabled);
		existingFlag.EnabledUsers = new List<string> { "old-user1", "old-user2" };
		existingFlag.DisabledUsers = new List<string> { "blocked-user" };
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			EnabledUsers = new List<string> { "new-user1", "new-user2", "admin@company.com" },
			DisabledUsers = new List<string> { "banned-user1", "banned-user2" }
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("users-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.EnabledUsers.Count.ShouldBe(3);
		okResult.Value.EnabledUsers.ShouldContain("admin@company.com");
		okResult.Value.EnabledUsers.ShouldNotContain("old-user1");
		okResult.Value.DisabledUsers.Count.ShouldBe(2);
		okResult.Value.DisabledUsers.ShouldContain("banned-user1");
		okResult.Value.DisabledUsers.ShouldNotContain("blocked-user");
	}

	[Fact]
	public async Task If_VariationsAndDefaultUpdated_ThenReplacesVariations()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("variations-flag", FeatureFlagStatus.Enabled);
		existingFlag.Variations = new Dictionary<string, object> { ["old"] = "value" };
		existingFlag.DefaultVariation = "old-default";
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			Variations = new Dictionary<string, object>
			{
				{ "on", new { version = "v2", features = new[] { "feature1", "feature2" } } },
				{ "off", false },
				{ "beta", "beta-config" }
			},
			DefaultVariation = "beta"
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("variations-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Variations.Count.ShouldBe(3);
		okResult.Value.Variations.ShouldContainKey("beta");
		okResult.Value.Variations.ShouldNotContainKey("old");
		okResult.Value.DefaultVariation.ShouldBe("beta");
	}
}

public class ModifyFlagHandler_NotFoundScenarios : IClassFixture<ModifyFlagHandlerFixture>
{
	private readonly ModifyFlagHandlerFixture _fixture;

	public ModifyFlagHandler_NotFoundScenarios(ModifyFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await _fixture.ClearAllData();

		var request = new ModifyFlagRequest
		{
			Name = "Updated Name"
		};

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

		var request = new ModifyFlagRequest { Name = "Test" };

		// Act
		var result = await _fixture.Handler.HandleAsync(key, request);

		// Assert
		result.ShouldBeOfType<NotFound<string>>();
		var notFoundResult = (NotFound<string>)result;
		notFoundResult.Value.ShouldBe($"Feature flag '{key}' not found");
	}
}

public class ModifyFlagHandler_NullFieldHandling : IClassFixture<ModifyFlagHandlerFixture>
{
	private readonly ModifyFlagHandlerFixture _fixture;

	public ModifyFlagHandler_NullFieldHandling(ModifyFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_AllFieldsNull_ThenNoChanges()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("null-fields-flag", FeatureFlagStatus.Enabled);
		existingFlag.Name = "Original Name";
		existingFlag.Description = "Original Description";
		existingFlag.PercentageEnabled = 50;
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			// All fields are null/default
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("null-fields-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Name.ShouldBe("Original Name"); // Unchanged
		okResult.Value.Description.ShouldBe("Original Description"); // Unchanged
		okResult.Value.Status.ShouldBe(FeatureFlagStatus.Enabled.ToString()); // Unchanged
		okResult.Value.PercentageEnabled.ShouldBe(50); // Unchanged
		okResult.Value.UpdatedBy.ShouldBe("test-user"); // Should still update user
	}

	[Fact]
	public async Task If_NullCollections_ThenKeepsExisting()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("collections-flag", FeatureFlagStatus.UserTargeted);
		existingFlag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule { Attribute = "test", Operator = TargetingOperator.In, Values = new List<string> { "value" } }
		};
		existingFlag.EnabledUsers = new List<string> { "user1", "user2" };
		existingFlag.Tags = new Dictionary<string, string> { ["team"] = "backend" };
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			Name = "Updated Name"
			// Collections are null
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("collections-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.Name.ShouldBe("Updated Name");
		okResult.Value.TargetingRules.Count.ShouldBe(1); // Unchanged
		okResult.Value.EnabledUsers.Count.ShouldBe(2); // Unchanged
		okResult.Value.Tags.Count.ShouldBe(1); // Unchanged
	}

	[Fact]
	public async Task If_EmptyCollections_ThenReplacesWithEmpty()
	{
		// Arrange
		await _fixture.ClearAllData();
		var existingFlag = _fixture.CreateTestFlag("empty-collections-flag", FeatureFlagStatus.UserTargeted);
		existingFlag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule { Attribute = "test", Operator = TargetingOperator.In, Values = new List<string> { "value" } }
		};
		existingFlag.EnabledUsers = new List<string> { "user1", "user2" };
		await _fixture.Repository.CreateAsync(existingFlag);

		var request = new ModifyFlagRequest
		{
			TargetingRules = new List<TargetingRule>(), // Empty list
			EnabledUsers = new List<string>() // Empty list
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("empty-collections-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();
		var okResult = (Ok<FeatureFlagDto>)result;
		
		okResult.Value.TargetingRules.Count.ShouldBe(0); // Cleared
		okResult.Value.EnabledUsers.Count.ShouldBe(0); // Cleared
	}
}

public class ModifyFlagHandler_CacheInvalidation : IClassFixture<ModifyFlagHandlerFixture>
{
	private readonly ModifyFlagHandlerFixture _fixture;

	public ModifyFlagHandler_CacheInvalidation(ModifyFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagModified_ThenInvalidatesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("cached-modify-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Pre-populate cache
		await _fixture.Cache.SetAsync("cached-modify-flag", flag);
		var cachedFlag = await _fixture.Cache.GetAsync("cached-modify-flag");
		cachedFlag.ShouldNotBeNull(); // Verify cache is populated

		var request = new ModifyFlagRequest
		{
			Name = "Modified Name"
		};

		// Act
		var result = await _fixture.Handler.HandleAsync("cached-modify-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagDto>>();

		// Verify cache was cleared
		var cacheAfterModify = await _fixture.Cache.GetAsync("cached-modify-flag");
		cacheAfterModify.ShouldBeNull();
	}

	[Fact]
	public async Task If_NonExistentFlagModified_ThenNoCacheOperation()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Pre-populate cache with unrelated flag
		var unrelatedFlag = _fixture.CreateTestFlag("unrelated-flag", FeatureFlagStatus.Enabled);
		await _fixture.Cache.SetAsync("unrelated-flag", unrelatedFlag);

		var request = new ModifyFlagRequest { Name = "Test" };

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

public class ModifyFlagHandler_ErrorScenarios : IClassFixture<ModifyFlagHandlerFixture>
{
	private readonly ModifyFlagHandlerFixture _fixture;

	public ModifyFlagHandler_ErrorScenarios(ModifyFlagHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_RepositoryThrowsException_ThenReturnsInternalServerError()
	{
		// Arrange
		await _fixture.ClearAllData();
		await _fixture.DisposeContainers();

		var request = new ModifyFlagRequest { Name = "Test" };

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
		var badHandler = new ModifyFlagHandler(_fixture.CurrentUserService, badRepository, _fixture.Cache, _fixture.MockLogger.Object);

		var request = new ModifyFlagRequest { Name = "Test" };

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
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error updating feature flag bad-connection-flag")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class ModifyFlagHandlerFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public ModifyFlagHandler Handler { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public CurrentUserService CurrentUserService { get; private set; } = null!;
	public Mock<ILogger<ModifyFlag>> MockLogger { get; }
	public Mock<ILogger<PostgreSQLFeatureFlagRepository>> MockRepositoryLogger { get; }
	
	private IConnectionMultiplexer _redisConnection = null!;

	public ModifyFlagHandlerFixture()
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

		MockLogger = new Mock<ILogger<ModifyFlag>>();
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
		Handler = new ModifyFlagHandler(CurrentUserService, Repository, Cache, MockLogger.Object);
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