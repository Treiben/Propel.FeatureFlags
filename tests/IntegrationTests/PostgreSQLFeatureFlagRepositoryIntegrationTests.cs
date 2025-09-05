using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.PostgresSql;
using Testcontainers.PostgreSql;

namespace FeatureFlags.IntegrationTests.Postgres;

public class GetAsync_WhenFlagExists : IClassFixture<PostgreSQLRepositoryFixture>
{
	private readonly PostgreSQLRepositoryFixture _fixture;

	public GetAsync_WhenFlagExists(PostgreSQLRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsFlag()
	{
		// Arrange
		var flag = _fixture.CreateTestFlag("get-test", FlagEvaluationMode.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.GetAsync("get-test");

		// Assert
		result.ShouldNotBeNull();
		result.Key.ShouldBe("get-test");
		result.Name.ShouldBe(flag.Name);
		result.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagWithComplexData_ThenDeserializesCorrectly()
	{
		// Arrange
		var flag = _fixture.CreateTestFlag("complex-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules =
		[
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = ["US", "CA"],
				Variation = "region-specific"
			}
		];
		flag.UserAccess = FlagUserAccessControl.CreateAccessControl(allowedUsers: ["user1", "user2"]);
		flag.Tags = new Dictionary<string, string> { { "team", "platform" }, { "env", "test" } };

		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.GetAsync("complex-flag");

		// Assert
		result.ShouldNotBeNull();
		result.TargetingRules.Count.ShouldBe(1);
		result.TargetingRules[0].Attribute.ShouldBe("region");
		result.TargetingRules[0].Values.ShouldContain("US");
		result.Tags.ShouldContainKeyAndValue("team", "platform");
	}
}

public class GetAsync_WhenFlagDoesNotExist : IClassFixture<PostgreSQLRepositoryFixture>
{
	private readonly PostgreSQLRepositoryFixture _fixture;

	public GetAsync_WhenFlagDoesNotExist(PostgreSQLRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsNull()
	{
		// Act
		var result = await _fixture.Repository.GetAsync("non-existent-flag");

		// Assert
		result.ShouldBeNull();
	}
}

public class GetAllAsync_WithMultipleFlags : IClassFixture<PostgreSQLRepositoryFixture>
{
	private readonly PostgreSQLRepositoryFixture _fixture;

	public GetAllAsync_WithMultipleFlags(PostgreSQLRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsAllFlagsOrderedByName()
	{
		// Arrange
		var flag1 = _fixture.CreateTestFlag("z-flag", FlagEvaluationMode.Enabled);
		flag1.Name = "Z Flag";
		var flag2 = _fixture.CreateTestFlag("a-flag", FlagEvaluationMode.Disabled);
		flag2.Name = "A Flag";

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);

		// Act
		var result = await _fixture.Repository.GetAllAsync();

		// Assert
		result.Count.ShouldBeGreaterThanOrEqualTo(2);
		var createdFlags = result.Where(f => f.Key.Contains("-flag")).ToList();
		createdFlags[0].Name.ShouldBe("A Flag");
		createdFlags[1].Name.ShouldBe("Z Flag");
	}

	[Fact]
	public async Task If_NoFlags_ThenReturnsEmptyList()
	{
		// Arrange
		await _fixture.ClearAllFlags();

		// Act
		var result = await _fixture.Repository.GetAllAsync();

		// Assert
		result.ShouldNotBeNull();
		result.Count.ShouldBe(0);
	}
}

public class GetPagedAsync_WithValidParameters : IClassFixture<PostgreSQLRepositoryFixture>
{
	private readonly PostgreSQLRepositoryFixture _fixture;

	public GetPagedAsync_WithValidParameters(PostgreSQLRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsPaginatedResults()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		for (int i = 1; i <= 5; i++)
		{
			var flag = _fixture.CreateTestFlag($"page-flag-{i:00}", FlagEvaluationMode.Enabled);
			flag.Name = $"Page Flag {i:00}";
			await _fixture.Repository.CreateAsync(flag);
		}

		// Act
		var result = await _fixture.Repository.GetPagedAsync(1, 3);

		// Assert
		result.Items.Count.ShouldBe(3);
		result.TotalCount.ShouldBe(5);
		result.Page.ShouldBe(1);
		result.PageSize.ShouldBe(3);
		result.TotalPages.ShouldBe(2);
	}

	[Fact]
	public async Task If_InvalidPageParameters_ThenNormalizesValues()
	{
		// Act
		var result = await _fixture.Repository.GetPagedAsync(-1, -5);

		// Assert
		result.Page.ShouldBe(1);
		result.PageSize.ShouldBe(10);
	}

	[Fact]
	public async Task If_PageSizeExceedsLimit_ThenCapsAt100()
	{
		// Act
		var result = await _fixture.Repository.GetPagedAsync(1, 150);

		// Assert
		result.PageSize.ShouldBe(100);
	}
}

public class GetPagedAsync_WithFilter : IClassFixture<PostgreSQLRepositoryFixture>
{
	private readonly PostgreSQLRepositoryFixture _fixture;

	public GetPagedAsync_WithFilter(PostgreSQLRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FilterByEvaluationModes_ThenReturnsMatchingFlags()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var enabledFlag = _fixture.CreateTestFlag("enabled-flag", FlagEvaluationMode.Enabled);
		var disabledFlag = _fixture.CreateTestFlag("disabled-flag", FlagEvaluationMode.Disabled);
		
		await _fixture.Repository.CreateAsync(enabledFlag);
		await _fixture.Repository.CreateAsync(disabledFlag);

		var filter = new FeatureFlagFilter { EvaluationModes = [FlagEvaluationMode.Enabled] };

		// Act
		var result = await _fixture.Repository.GetPagedAsync(1, 10, filter);

		// Assert
		result.Items.All(f => f.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled])).ShouldBeTrue();
		result.Items.Any(f => f.Key == "enabled-flag").ShouldBeTrue();
		result.Items.Any(f => f.Key == "disabled-flag").ShouldBeFalse();
	}

	[Fact]
	public async Task If_FilterByTags_ThenReturnsMatchingFlags()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag1 = _fixture.CreateTestFlag("tag-flag-1", FlagEvaluationMode.Enabled);
		flag1.Tags = new Dictionary<string, string> { { "team", "backend" } };
		var flag2 = _fixture.CreateTestFlag("tag-flag-2", FlagEvaluationMode.Enabled);
		flag2.Tags = new Dictionary<string, string> { { "team", "frontend" } };

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);

		var filter = new FeatureFlagFilter 
		{ 
			Tags = new Dictionary<string, string> { { "team", "backend" } }
		};

		// Act
		var result = await _fixture.Repository.GetPagedAsync(1, 10, filter);

		// Assert
		result.Items.Any(f => f.Key == "tag-flag-1").ShouldBeTrue();
		result.Items.Any(f => f.Key == "tag-flag-2").ShouldBeFalse();
	}

	[Fact]
	public async Task If_FilterByMultipleEvaluationModes_ThenReturnsMatchingFlags()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		
		// Create flag with all specified modes
		var multiModeFlag = _fixture.CreateTestFlag("multi-mode-flag", FlagEvaluationMode.Disabled);
		multiModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		multiModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		multiModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);
		multiModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserRolloutPercentage);
		multiModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.TenantRolloutPercentage);

		// Create flag with only some modes
		var partialModeFlag = _fixture.CreateTestFlag("partial-mode-flag", FlagEvaluationMode.Disabled);
		partialModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		partialModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);

		// Create flag with different modes
		var otherModeFlag = _fixture.CreateTestFlag("other-mode-flag", FlagEvaluationMode.Enabled);

		await _fixture.Repository.CreateAsync(multiModeFlag);
		await _fixture.Repository.CreateAsync(partialModeFlag);
		await _fixture.Repository.CreateAsync(otherModeFlag);

		// Test filtering by single mode
		var singleModeFilter = new FeatureFlagFilter { EvaluationModes = [FlagEvaluationMode.UserTargeted] };
		var singleModeResult = await _fixture.Repository.GetPagedAsync(1, 10, singleModeFilter);

		singleModeResult.Items.Any(f => f.Key == "multi-mode-flag").ShouldBeTrue();
		singleModeResult.Items.Any(f => f.Key == "partial-mode-flag").ShouldBeTrue();
		singleModeResult.Items.Any(f => f.Key == "other-mode-flag").ShouldBeFalse();

		// Test filtering by two modes
		var twoModeFilter = new FeatureFlagFilter 
		{ 
			EvaluationModes = [FlagEvaluationMode.Scheduled, FlagEvaluationMode.TimeWindow] 
		};
		var twoModeResult = await _fixture.Repository.GetPagedAsync(1, 10, twoModeFilter);

		twoModeResult.Items.Any(f => f.Key == "multi-mode-flag").ShouldBeTrue();
		twoModeResult.Items.Any(f => f.Key == "partial-mode-flag").ShouldBeTrue(); // Has Scheduled
		twoModeResult.Items.Any(f => f.Key == "other-mode-flag").ShouldBeFalse();

		// Test filtering by all specified modes
		var allModeFilter = new FeatureFlagFilter 
		{ 
			EvaluationModes = [
				FlagEvaluationMode.Scheduled,
				FlagEvaluationMode.TimeWindow,
				FlagEvaluationMode.UserTargeted,
				FlagEvaluationMode.UserRolloutPercentage,
				FlagEvaluationMode.TenantRolloutPercentage
			] 
		};
		var allModeResult = await _fixture.Repository.GetPagedAsync(1, 10, allModeFilter);

		allModeResult.Items.Any(f => f.Key == "multi-mode-flag").ShouldBeTrue();
		allModeResult.Items.Any(f => f.Key == "partial-mode-flag").ShouldBeTrue(); // Has some of the modes
		allModeResult.Items.Any(f => f.Key == "other-mode-flag").ShouldBeFalse();
	}
}

public class CreateAsync_WithValidFlag : IClassFixture<PostgreSQLRepositoryFixture>
{
	private readonly PostgreSQLRepositoryFixture _fixture;

	public CreateAsync_WithValidFlag(PostgreSQLRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenCreatesFlag()
	{
		// Arrange
		var flag = _fixture.CreateTestFlag("create-test", FlagEvaluationMode.Enabled);

		// Act
		var result = await _fixture.Repository.CreateAsync(flag);

		// Assert
		result.ShouldBe(flag);
		var retrieved = await _fixture.Repository.GetAsync("create-test");
		retrieved.ShouldNotBeNull();
		retrieved.Key.ShouldBe("create-test");
	}

	[Fact]
	public async Task If_FlagWithSchedule_ThenCreatesCorrectly()
	{
		// Arrange
		var flag = _fixture.CreateTestFlag("scheduled-flag", FlagEvaluationMode.Scheduled);
		flag.Schedule = FlagActivationSchedule.CreateSchedule(
			DateTime.UtcNow.AddDays(1), 
			DateTime.UtcNow.AddDays(7));

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("scheduled-flag");
		retrieved.ShouldNotBeNull();
		retrieved.Schedule.ScheduledEnableDate.ShouldNotBeNull();
		retrieved.Schedule.ScheduledDisableDate.ShouldNotBeNull();
	}

	[Fact]
	public async Task If_FlagWithOperationalWindow_ThenCreatesCorrectly()
	{
		// Arrange
		var flag = _fixture.CreateTestFlag("window-flag", FlagEvaluationMode.TimeWindow);
		flag.OperationalWindow = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17),
			"America/New_York",
			[DayOfWeek.Monday, DayOfWeek.Friday]);

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("window-flag");
		retrieved.ShouldNotBeNull();
		retrieved.OperationalWindow.WindowStartTime.ShouldBe(TimeSpan.FromHours(9));
		retrieved.OperationalWindow.TimeZone.ShouldBe("America/New_York");
		retrieved.OperationalWindow.WindowDays.ShouldContain(DayOfWeek.Monday);
	}
}

public class UpdateAsync_WithExistingFlag : IClassFixture<PostgreSQLRepositoryFixture>
{
	private readonly PostgreSQLRepositoryFixture _fixture;

	public UpdateAsync_WithExistingFlag(PostgreSQLRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenUpdatesFlag()
	{
		// Arrange
		var flag = _fixture.CreateTestFlag("update-test", FlagEvaluationMode.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		flag.Name = "Updated Name";
		flag.Description = "Updated Description";
		flag.EvaluationModeSet.RemoveMode(FlagEvaluationMode.Enabled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);

		// Act
		var result = await _fixture.Repository.UpdateAsync(flag);

		// Assert
		result.ShouldBe(flag);
		var retrieved = await _fixture.Repository.GetAsync("update-test");
		retrieved.ShouldNotBeNull();
		retrieved.Name.ShouldBe("Updated Name");
		retrieved.Description.ShouldBe("Updated Description");
		retrieved.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Disabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var flag = _fixture.CreateTestFlag("non-existent-update", FlagEvaluationMode.Enabled);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() => _fixture.Repository.UpdateAsync(flag));
	}
}

public class DeleteAsync_WhenFlagExists : IClassFixture<PostgreSQLRepositoryFixture>
{
	private readonly PostgreSQLRepositoryFixture _fixture;

	public DeleteAsync_WhenFlagExists(PostgreSQLRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenDeletesFlagAndReturnsTrue()
	{
		// Arrange
		var flag = _fixture.CreateTestFlag("delete-test", FlagEvaluationMode.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.DeleteAsync("delete-test");

		// Assert
		result.ShouldBeTrue();
		var retrieved = await _fixture.Repository.GetAsync("delete-test");
		retrieved.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsFalse()
	{
		// Act
		var result = await _fixture.Repository.DeleteAsync("non-existent-delete");

		// Assert
		result.ShouldBeFalse();
	}
}

public class GetExpiringAsync_WithExpiringFlags : IClassFixture<PostgreSQLRepositoryFixture>
{
	private readonly PostgreSQLRepositoryFixture _fixture;

	public GetExpiringAsync_WithExpiringFlags(PostgreSQLRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsExpiringNonPermanentFlags()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var expiringFlag = _fixture.CreateTestFlag("expiring-flag", FlagEvaluationMode.Enabled);
		expiringFlag.ExpirationDate = DateTime.UtcNow.AddDays(1);
		expiringFlag.IsPermanent = false;

		var permanentFlag = _fixture.CreateTestFlag("permanent-flag", FlagEvaluationMode.Enabled);
		permanentFlag.ExpirationDate = DateTime.UtcNow.AddDays(1);
		permanentFlag.IsPermanent = true;

		var nonExpiringFlag = _fixture.CreateTestFlag("non-expiring-flag", FlagEvaluationMode.Enabled);
		nonExpiringFlag.ExpirationDate = DateTime.UtcNow.AddDays(10);
		nonExpiringFlag.IsPermanent = false;

		await _fixture.Repository.CreateAsync(expiringFlag);
		await _fixture.Repository.CreateAsync(permanentFlag);
		await _fixture.Repository.CreateAsync(nonExpiringFlag);

		// Act
		var result = await _fixture.Repository.GetExpiringAsync(DateTime.UtcNow.AddDays(2));

		// Assert
		result.Any(f => f.Key == "expiring-flag").ShouldBeTrue();
		result.Any(f => f.Key == "permanent-flag").ShouldBeFalse();
		result.Any(f => f.Key == "non-expiring-flag").ShouldBeFalse();
	}

	[Fact]
	public async Task If_NoExpiringFlags_ThenReturnsEmptyList()
	{
		// Arrange
		await _fixture.ClearAllFlags();

		// Act
		var result = await _fixture.Repository.GetExpiringAsync(DateTime.UtcNow.AddDays(1));

		// Assert
		result.ShouldBeEmpty();
	}
}

public class GetByTagsAsync_WithTagFilters : IClassFixture<PostgreSQLRepositoryFixture>
{
	private readonly PostgreSQLRepositoryFixture _fixture;

	public GetByTagsAsync_WithTagFilters(PostgreSQLRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ExactKeyValueMatch_ThenReturnsMatchingFlags()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag1 = _fixture.CreateTestFlag("tagged-flag-1", FlagEvaluationMode.Enabled);
		flag1.Tags = new Dictionary<string, string> { { "environment", "production" }, { "team", "backend" } };

		var flag2 = _fixture.CreateTestFlag("tagged-flag-2", FlagEvaluationMode.Enabled);
		flag2.Tags = new Dictionary<string, string> { { "environment", "staging" }, { "team", "backend" } };

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);

		// Act
		var result = await _fixture.Repository.GetByTagsAsync(
			new Dictionary<string, string> { { "environment", "production" } });

		// Assert
		result.Any(f => f.Key == "tagged-flag-1").ShouldBeTrue();
		result.Any(f => f.Key == "tagged-flag-2").ShouldBeFalse();
	}

	[Fact]
	public async Task If_KeyOnlySearch_ThenReturnsAllFlagsWithKey()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag1 = _fixture.CreateTestFlag("key-search-1", FlagEvaluationMode.Enabled);
		flag1.Tags = new Dictionary<string, string> { { "team", "backend" } };

		var flag2 = _fixture.CreateTestFlag("key-search-2", FlagEvaluationMode.Enabled);
		flag2.Tags = new Dictionary<string, string> { { "team", "frontend" } };

		var flag3 = _fixture.CreateTestFlag("key-search-3", FlagEvaluationMode.Enabled);
		flag3.Tags = new Dictionary<string, string> { { "environment", "production" } };

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);
		await _fixture.Repository.CreateAsync(flag3);

		// Act
		var result = await _fixture.Repository.GetByTagsAsync(
			new Dictionary<string, string> { { "team", string.Empty } });

		// Assert
		result.Any(f => f.Key == "key-search-1").ShouldBeTrue();
		result.Any(f => f.Key == "key-search-2").ShouldBeTrue();
		result.Any(f => f.Key == "key-search-3").ShouldBeFalse();
	}

	[Fact]
	public async Task If_NoMatchingTags_ThenReturnsEmptyList()
	{
		// Arrange
		await _fixture.ClearAllFlags();

		// Act
		var result = await _fixture.Repository.GetByTagsAsync(
			new Dictionary<string, string> { { "nonexistent", "tag" } });

		// Assert
		result.ShouldBeEmpty();
	}
}

public class PostgreSQLRepositoryFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _container;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	private readonly ILogger<PostgreSQLFeatureFlagRepository> _logger;

	public PostgreSQLRepositoryFixture()
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:15-alpine")
			.WithDatabase("feature_flags_test")
			.WithUsername("test_user")
			.WithPassword("test_password")
			.WithPortBinding(5432, true)
			.Build();

		_logger = new Mock<ILogger<PostgreSQLFeatureFlagRepository>>().Object;
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();
		var connectionString = _container.GetConnectionString();

		Repository = new PostgreSQLFeatureFlagRepository(connectionString, _logger);

		// Create the feature_flags table
		await CreateFeatureFlagsTable(connectionString);
	}

	public async Task DisposeAsync()
	{
		await _container.DisposeAsync();
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

	public async Task ClearAllFlags()
	{
		var connectionString = _container.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();
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