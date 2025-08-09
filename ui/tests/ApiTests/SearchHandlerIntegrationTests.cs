using FeatureRabbit.Flags.Cache.Redis;
using FeatureRabbit.Flags.Core;
using FeatureRabbit.Flags.Persistence.PostgresSQL;
using FeatureRabbit.Management.Api.Endpoints;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ApiTests.SearchHandlerTests;

/* The tests cover these scenarios with SearchHandler integration:
 *		Tag-based searching with key:value format
 *		Tag-based searching with key-only format
 *		Status-based filtering with various flag statuses
 *		Combined tag and status filtering
 *		Case-insensitive status parsing
 *		No filters (returns all flags)
 *		Empty result scenarios
 *		Invalid status values handling
 *		Complex tag scenarios and edge cases
 *		Database persistence verification
 *		Error scenarios and exception handling
 *		Large dataset filtering performance
*/

public class SearchHandler_TagBasedSearch : IClassFixture<SearchHandlerFixture>
{
	private readonly SearchHandlerFixture _fixture;

	public SearchHandler_TagBasedSearch(SearchHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_TagWithKeyValue_ThenReturnsMatchingFlags()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag1 = _fixture.CreateTestFlag("backend-flag", FeatureFlagStatus.Enabled);
		flag1.Tags = new Dictionary<string, string> { ["team"] = "backend", ["priority"] = "high" };
		
		var flag2 = _fixture.CreateTestFlag("frontend-flag", FeatureFlagStatus.Disabled);
		flag2.Tags = new Dictionary<string, string> { ["team"] = "frontend", ["priority"] = "medium" };
		
		var flag3 = _fixture.CreateTestFlag("backend-flag2", FeatureFlagStatus.Percentage);
		flag3.Tags = new Dictionary<string, string> { ["team"] = "backend", ["priority"] = "low" };

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);
		await _fixture.Repository.CreateAsync(flag3);

		// Act
		var result = await _fixture.Handler.HandleAsync("team:backend");

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(2);
		okResult.Value.ShouldContain(f => f.Key == "backend-flag");
		okResult.Value.ShouldContain(f => f.Key == "backend-flag2");
		okResult.Value.ShouldNotContain(f => f.Key == "frontend-flag");
	}

	[Fact]
	public async Task If_TagWithKeyOnly_ThenReturnsMatchingFlags()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag1 = _fixture.CreateTestFlag("priority-flag", FeatureFlagStatus.Enabled);
		flag1.Tags = new Dictionary<string, string> { ["priority"] = "high", ["team"] = "backend" };
		
		var flag2 = _fixture.CreateTestFlag("no-priority-flag", FeatureFlagStatus.Disabled);
		flag2.Tags = new Dictionary<string, string> { ["team"] = "frontend" };
		
		var flag3 = _fixture.CreateTestFlag("low-priority-flag", FeatureFlagStatus.Enabled);
		flag3.Tags = new Dictionary<string, string> { ["priority"] = "low" };

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);
		await _fixture.Repository.CreateAsync(flag3);

		// Act
		var result = await _fixture.Handler.HandleAsync(tag: "priority");

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(2);
		okResult.Value.ShouldContain(f => f.Key == "priority-flag");
		okResult.Value.ShouldContain(f => f.Key == "low-priority-flag");
		okResult.Value.ShouldNotContain(f => f.Key == "no-priority-flag");
	}

	[Fact]
	public async Task If_TagWithMultipleColons_ThenHandlesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("complex-tag-flag", FeatureFlagStatus.Enabled);
		flag.Tags = new Dictionary<string, string> { ["url"] = "https://example.com:8080/path" };

		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("url:https://example.com:8080/path");

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		okResult.Value[0].Key.ShouldBe("complex-tag-flag");
		okResult.Value[0].Tags["url"].ShouldBe("https://example.com:8080/path");
	}

	[Fact]
	public async Task If_NonExistentTag_ThenReturnsEmpty()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("existing-flag", FeatureFlagStatus.Enabled);
		flag.Tags = new Dictionary<string, string> { ["team"] = "backend" };
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("team:frontend");

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		okResult.Value.Count.ShouldBe(0);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(":")]
	[InlineData("key:")]
	[InlineData(":value")]
	public async Task If_EdgeCaseTagFormats_ThenHandlesGracefully(string tag)
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("test-flag", FeatureFlagStatus.Enabled);
		flag.Tags = new Dictionary<string, string> { ["test"] = "value" };
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync(tag);

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		// Should not throw exception, behavior depends on repository implementation
	}
}

public class SearchHandler_StatusBasedFiltering : IClassFixture<SearchHandlerFixture>
{
	private readonly SearchHandlerFixture _fixture;

	public SearchHandler_StatusBasedFiltering(SearchHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Theory]
	[InlineData("Enabled", FeatureFlagStatus.Enabled)]
	[InlineData("Disabled", FeatureFlagStatus.Disabled)]
	[InlineData("Scheduled", FeatureFlagStatus.Scheduled)]
	[InlineData("Percentage", FeatureFlagStatus.Percentage)]
	[InlineData("UserTargeted", FeatureFlagStatus.UserTargeted)]
	[InlineData("TimeWindow", FeatureFlagStatus.TimeWindow)]
	public async Task If_ValidStatus_ThenReturnsMatchingFlags(string statusString, FeatureFlagStatus expectedStatus)
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var enabledFlag = _fixture.CreateTestFlag("enabled-flag", FeatureFlagStatus.Enabled);
		var disabledFlag = _fixture.CreateTestFlag("disabled-flag", FeatureFlagStatus.Disabled);
		var targetFlag = _fixture.CreateTestFlag("target-flag", expectedStatus);

		await _fixture.Repository.CreateAsync(enabledFlag);
		await _fixture.Repository.CreateAsync(disabledFlag);
		await _fixture.Repository.CreateAsync(targetFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync(null, statusString);

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBeGreaterThanOrEqualTo(1);
		okResult.Value.ShouldContain(f => f.Key == "target-flag");
		okResult.Value.All(f => f.Status == expectedStatus.ToString()).ShouldBeTrue();
	}

	[Theory]
	[InlineData("enabled")]
	[InlineData("ENABLED")]
	[InlineData("Enabled")]
	[InlineData("eNaBlEd")]
	public async Task If_CaseInsensitiveStatus_ThenReturnsMatchingFlags(string statusString)
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var enabledFlag = _fixture.CreateTestFlag("enabled-flag", FeatureFlagStatus.Enabled);
		var disabledFlag = _fixture.CreateTestFlag("disabled-flag", FeatureFlagStatus.Disabled);

		await _fixture.Repository.CreateAsync(enabledFlag);
		await _fixture.Repository.CreateAsync(disabledFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync(null, statusString);

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		okResult.Value[0].Key.ShouldBe("enabled-flag");
		okResult.Value[0].Status.ShouldBe(FeatureFlagStatus.Enabled.ToString());
	}

	[Theory]
	[InlineData("InvalidStatus")]
	[InlineData("NotAStatus")]
	[InlineData("123")]
	[InlineData("")]
	[InlineData("   ")]
	public async Task If_InvalidStatus_ThenIgnoresStatusFilter(string invalidStatus)
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var enabledFlag = _fixture.CreateTestFlag("enabled-flag", FeatureFlagStatus.Enabled);
		var disabledFlag = _fixture.CreateTestFlag("disabled-flag", FeatureFlagStatus.Disabled);

		await _fixture.Repository.CreateAsync(enabledFlag);
		await _fixture.Repository.CreateAsync(disabledFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync(null, invalidStatus);

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		// Should return all flags when status filter is invalid
		okResult.Value.Count.ShouldBe(2);
		okResult.Value.ShouldContain(f => f.Key == "enabled-flag");
		okResult.Value.ShouldContain(f => f.Key == "disabled-flag");
	}
}

public class SearchHandler_CombinedFiltering : IClassFixture<SearchHandlerFixture>
{
	private readonly SearchHandlerFixture _fixture;

	public SearchHandler_CombinedFiltering(SearchHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_TagAndStatus_ThenAppliesBothFilters()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var backendEnabledFlag = _fixture.CreateTestFlag("backend-enabled", FeatureFlagStatus.Enabled);
		backendEnabledFlag.Tags = new Dictionary<string, string> { ["team"] = "backend" };
		
		var backendDisabledFlag = _fixture.CreateTestFlag("backend-disabled", FeatureFlagStatus.Disabled);
		backendDisabledFlag.Tags = new Dictionary<string, string> { ["team"] = "backend" };
		
		var frontendEnabledFlag = _fixture.CreateTestFlag("frontend-enabled", FeatureFlagStatus.Enabled);
		frontendEnabledFlag.Tags = new Dictionary<string, string> { ["team"] = "frontend" };

		await _fixture.Repository.CreateAsync(backendEnabledFlag);
		await _fixture.Repository.CreateAsync(backendDisabledFlag);
		await _fixture.Repository.CreateAsync(frontendEnabledFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync("team:backend", "Enabled");

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		okResult.Value[0].Key.ShouldBe("backend-enabled");
		okResult.Value[0].Status.ShouldBe(FeatureFlagStatus.Enabled.ToString());
		okResult.Value[0].Tags["team"].ShouldBe("backend");
	}

	[Fact]
	public async Task If_TagAndInvalidStatus_ThenOnlyAppliesTagFilter()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var backendEnabledFlag = _fixture.CreateTestFlag("backend-enabled", FeatureFlagStatus.Enabled);
		backendEnabledFlag.Tags = new Dictionary<string, string> { ["team"] = "backend" };
		
		var backendDisabledFlag = _fixture.CreateTestFlag("backend-disabled", FeatureFlagStatus.Disabled);
		backendDisabledFlag.Tags = new Dictionary<string, string> { ["team"] = "backend" };

		await _fixture.Repository.CreateAsync(backendEnabledFlag);
		await _fixture.Repository.CreateAsync(backendDisabledFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync("team:backend", "InvalidStatus");

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(2);
		okResult.Value.ShouldContain(f => f.Key == "backend-enabled");
		okResult.Value.ShouldContain(f => f.Key == "backend-disabled");
		okResult.Value.All(f => f.Tags["team"] == "backend").ShouldBeTrue();
	}

	[Fact]
	public async Task If_NoMatchingTagButValidStatus_ThenReturnsEmpty()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var backendEnabledFlag = _fixture.CreateTestFlag("backend-enabled", FeatureFlagStatus.Enabled);
		backendEnabledFlag.Tags = new Dictionary<string, string> { ["team"] = "backend" };

		await _fixture.Repository.CreateAsync(backendEnabledFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync("team:frontend", "Enabled");

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		okResult.Value.Count.ShouldBe(0);
	}
}

public class SearchHandler_NoFilters : IClassFixture<SearchHandlerFixture>
{
	private readonly SearchHandlerFixture _fixture;

	public SearchHandler_NoFilters(SearchHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_NoFilters_ThenReturnsAllFlags()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag1 = _fixture.CreateTestFlag("flag1", FeatureFlagStatus.Enabled);
		var flag2 = _fixture.CreateTestFlag("flag2", FeatureFlagStatus.Disabled);
		var flag3 = _fixture.CreateTestFlag("flag3", FeatureFlagStatus.Percentage);

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);
		await _fixture.Repository.CreateAsync(flag3);

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(3);
		okResult.Value.ShouldContain(f => f.Key == "flag1");
		okResult.Value.ShouldContain(f => f.Key == "flag2");
		okResult.Value.ShouldContain(f => f.Key == "flag3");
	}

	[Fact]
	public async Task If_NullParameters_ThenReturnsAllFlags()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("test-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync(null, null);

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		okResult.Value[0].Key.ShouldBe("test-flag");
	}

	[Fact]
	public async Task If_EmptyDatabase_ThenReturnsEmptyList()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		okResult.Value.Count.ShouldBe(0);
	}
}

public class SearchHandler_ComplexScenarios : IClassFixture<SearchHandlerFixture>
{
	private readonly SearchHandlerFixture _fixture;

	public SearchHandler_ComplexScenarios(SearchHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagsWithComplexData_ThenReturnsCorrectDtos()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var complexFlag = _fixture.CreateTestFlag("complex-flag", FeatureFlagStatus.UserTargeted);
		complexFlag.Tags = new Dictionary<string, string>
		{
			["team"] = "backend",
			["environment"] = "production",
			["priority"] = "critical"
		};
		complexFlag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "country",
				Operator = TargetingOperator.In,
				Values = new List<string> { "US" },
				Variation = "us-version"
			}
		};
		complexFlag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium" },
			{ "off", "basic" }
		};

		await _fixture.Repository.CreateAsync(complexFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync("team:backend");

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		var dto = okResult.Value[0];
		
		dto.Key.ShouldBe("complex-flag");
		dto.Status.ShouldBe(FeatureFlagStatus.UserTargeted.ToString());
		dto.Tags.Count.ShouldBe(3);
		dto.Tags["team"].ShouldBe("backend");
		dto.Tags["priority"].ShouldBe("critical");
		dto.TargetingRules.Count.ShouldBe(1);
		dto.Variations.Count.ShouldBe(2);
	}

	[Fact]
	public async Task If_LargeNumberOfFlags_ThenPerformsEfficiently()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		for (int i = 0; i < 100; i++)
		{
			var flag = _fixture.CreateTestFlag($"flag-{i}", i % 2 == 0 ? FeatureFlagStatus.Enabled : FeatureFlagStatus.Disabled);
			flag.Tags = new Dictionary<string, string>
			{
				["batch"] = "performance-test",
				["index"] = i.ToString()
			};
			await _fixture.Repository.CreateAsync(flag);
		}

		// Act
		var result = await _fixture.Handler.HandleAsync("batch:performance-test", "Enabled");

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(50); // Half are enabled
		okResult.Value.All(f => f.Status == FeatureFlagStatus.Enabled.ToString()).ShouldBeTrue();
		okResult.Value.All(f => f.Tags["batch"] == "performance-test").ShouldBeTrue();
	}

	[Fact]
	public async Task If_SpecialCharactersInTags_ThenHandlesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("special-char-flag", FeatureFlagStatus.Enabled);
		flag.Tags = new Dictionary<string, string>
		{
			["special-chars"] = "!@#$%^&*()_+-={}[]|\\:;\"'<>?,./"
		};

		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync("special-chars:!@#$%^&*()_+-={}[]|\\:;\"'<>?,./");

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		okResult.Value[0].Key.ShouldBe("special-char-flag");
	}
}

public class SearchHandler_ErrorScenarios : IClassFixture<SearchHandlerFixture>
{
	private readonly SearchHandlerFixture _fixture;

	public SearchHandler_ErrorScenarios(SearchHandlerFixture fixture)
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
		var result = await _fixture.Handler.HandleAsync("team:backend");

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
		var badRepository = new PostgreSQLFeatureFlagRepository(badConnectionString, new Mock<ILogger<PostgreSQLFeatureFlagRepository>>().Object);
		var badHandler = new SearchHandler(badRepository, _fixture.MockLogger.Object);

		// Act
		var result = await badHandler.HandleAsync("team:backend");

		// Assert
		result.ShouldBeOfType<StatusCodeHttpResult>();
		var statusResult = (StatusCodeHttpResult)result;
		statusResult.StatusCode.ShouldBe(500);

		// Verify error was logged
		_fixture.MockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error searching flags")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}

public class SearchHandlerFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public SearchHandler Handler { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public Mock<ILogger<SearchHandler>> MockLogger { get; }
	
	private IConnectionMultiplexer _redisConnection = null!;

	public SearchHandlerFixture()
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

		MockLogger = new Mock<ILogger<SearchHandler>>();
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

		// Setup Handler
		Handler = new SearchHandler(Repository, MockLogger.Object);
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