using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.SqlServer;
using Testcontainers.MsSql;

namespace FeatureFlags.IntegrationTests.SqlServer;

/* The tests cover these scenarios:
 *		CRUD operations with various flag configurations
 *		Complex data serialization/deserialization
 *		Time-based functionality
 *		Tag-based queries
 *		Error handling and edge cases
 *		Cancellation token support
 *		Database-specific error scenarios
*/

public class CreateAsync_WithValidFlag : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public CreateAsync_WithValidFlag(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenCreatesFlag()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("create-test", FeatureFlagStatus.Enabled);

		// Act
		var result = await _fixture.Repository.CreateAsync(flag);

		// Assert
		result.ShouldBe(flag);

		// Verify it's actually in the database
		var retrieved = await _fixture.Repository.GetAsync("create-test");
		retrieved.ShouldNotBeNull();
		retrieved.Key.ShouldBe("create-test");
	}

	[Fact]
	public async Task If_FlagWithTimeData_ThenStoresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("time-flag", FeatureFlagStatus.TimeWindow);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(30);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(1);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddDays(7);
		flag.WindowStartTime = TimeSpan.FromHours(9); // 9 AM
		flag.WindowEndTime = TimeSpan.FromHours(17); // 5 PM
		flag.TimeZone = "America/New_York";

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("time-flag");
		retrieved.ShouldNotBeNull();
		retrieved.ExpirationDate.ShouldNotBeNull();
		retrieved.ExpirationDate.Value.ShouldBeInRange(flag.ExpirationDate.Value.AddSeconds(-1), flag.ExpirationDate.Value.AddSeconds(1));
		retrieved.ScheduledEnableDate.ShouldNotBeNull();
		retrieved.ScheduledEnableDate.Value.ShouldBeInRange(flag.ScheduledEnableDate.Value.AddSeconds(-1), flag.ScheduledEnableDate.Value.AddSeconds(1));
		retrieved.ScheduledDisableDate.ShouldNotBeNull();
		retrieved.ScheduledDisableDate.Value.ShouldBeInRange(flag.ScheduledDisableDate.Value.AddSeconds(-1), flag.ScheduledDisableDate.Value.AddSeconds(1));
		retrieved.WindowStartTime.ShouldBe(flag.WindowStartTime);
		retrieved.WindowEndTime.ShouldBe(flag.WindowEndTime);
		retrieved.TimeZone.ShouldBe("America/New_York");
	}

	[Fact]
	public async Task If_FlagWithPercentageRollout_ThenStoresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("percentage-flag", FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 75;

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("percentage-flag");
		retrieved.ShouldNotBeNull();
		retrieved.PercentageEnabled.ShouldBe(75);
	}
}

public class When_FlagExists : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public When_FlagExists(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_UpdateWithBasicData_ThenUpdatesFlag()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("update-test", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var originalUpdatedAt = flag.UpdatedAt;
		await Task.Delay(10); // Ensure time difference

		flag.Status = FeatureFlagStatus.Enabled;
		flag.Name = "Updated Name";
		flag.Description = "Updated Description";
		flag.UpdatedBy = "updater";

		// Act
		var result = await _fixture.Repository.UpdateAsync(flag);

		// Assert
		result.ShouldBe(flag);
		result.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);

		// Verify changes in database
		var retrieved = await _fixture.Repository.GetAsync("update-test");
		retrieved.ShouldNotBeNull();
		retrieved.Status.ShouldBe(FeatureFlagStatus.Enabled);
		retrieved.Name.ShouldBe("Updated Name");
		retrieved.Description.ShouldBe("Updated Description");
		retrieved.UpdatedBy.ShouldBe("updater");
		retrieved.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);
	}

	[Fact]
	public async Task If_UpdateWithFlagComplexData_ThenStoresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("complex-update", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = new List<string> { "US", "CA", "UK" },
				Variation = "region-specific"
			}
		};
		flag.EnabledUsers = new List<string> { "admin1", "admin2" };
		flag.Variations = new Dictionary<string, object>
		{
			{ "region-specific", new { currency = "USD", language = "en" } }
		};

		// Act
		await _fixture.Repository.UpdateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("complex-update");
		retrieved.ShouldNotBeNull();
		retrieved.TargetingRules.Count.ShouldBe(1);
		retrieved.TargetingRules[0].Attribute.ShouldBe("region");
		retrieved.TargetingRules[0].Values.ShouldContain("US");
		retrieved.TargetingRules[0].Values.ShouldContain("CA");
		retrieved.TargetingRules[0].Values.ShouldContain("UK");
		retrieved.EnabledUsers.ShouldContain("admin1");
		retrieved.EnabledUsers.ShouldContain("admin2");
		retrieved.Variations.ShouldContainKey("region-specific");
	}

	[Fact]
	public async Task If_Delete_ThenDeletesFlagAndReturnsTrue()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("delete-test", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.DeleteAsync("delete-test");

		// Assert
		result.ShouldBeTrue();

		// Verify it's actually deleted
		var retrieved = await _fixture.Repository.GetAsync("delete-test");
		retrieved.ShouldBeNull();
	}

	[Fact]
	public async Task If_GetWhenFlagEnabled_ThenReturnsFlagWithEnabledStatus()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("test-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.GetAsync("test-flag");

		// Assert
		result.ShouldNotBeNull();
		result.Key.ShouldBe("test-flag");
		result.Name.ShouldBe(flag.Name);
		result.Status.ShouldBe(FeatureFlagStatus.Enabled);
		result.CreatedBy.ShouldBe(flag.CreatedBy);
		result.UpdatedBy.ShouldBe(flag.UpdatedBy);
	}

	[Fact]
	public async Task If_GetWhenFlagDisabled_ThenReturnsFlagWithDisabledStatus()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("test-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.GetAsync("test-flag");

		// Assert
		result.ShouldNotBeNull();
		result.Key.ShouldBe("test-flag");
		result.Name.ShouldBe(flag.Name);
		result.Status.ShouldBe(FeatureFlagStatus.Disabled);
		result.CreatedBy.ShouldBe(flag.CreatedBy);
		result.UpdatedBy.ShouldBe(flag.UpdatedBy);
	}

	[Fact]
	public async Task If_GetAndFlagHasComplexData_ThenDeserializesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("complex-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "userType",
				Operator = TargetingOperator.Equals,
				Values = new List<string> { "premium", "enterprise" },
				Variation = "premium-variation"
			}
		};
		flag.EnabledUsers = new List<string> { "user1", "user2" };
		flag.DisabledUsers = new List<string> { "user3", "user4" };
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "enabled-value" },
			{ "off", "disabled-value" },
			{ "premium", new { feature = "advanced", limit = 1000 } }
		};
		flag.Tags = new Dictionary<string, string>
		{
			{ "team", "platform" },
			{ "environment", "production" }
		};
		flag.WindowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday };

		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.GetAsync("complex-flag");

		// Assert
		result.ShouldNotBeNull();
		result.TargetingRules.Count.ShouldBe(1);
		result.TargetingRules[0].Attribute.ShouldBe("userType");
		result.TargetingRules[0].Operator.ShouldBe(TargetingOperator.Equals);
		result.TargetingRules[0].Values.ShouldContain("premium");
		result.TargetingRules[0].Values.ShouldContain("enterprise");

		result.EnabledUsers.ShouldContain("user1");
		result.EnabledUsers.ShouldContain("user2");
		result.DisabledUsers.ShouldContain("user3");
		result.DisabledUsers.ShouldContain("user4");

		result.Variations.ShouldContainKey("on");
		result.Variations.ShouldContainKey("premium");

		result.Tags.ShouldContainKeyAndValue("team", "platform");
		result.Tags.ShouldContainKeyAndValue("environment", "production");

		result.WindowDays.ShouldContain(DayOfWeek.Monday);
		result.WindowDays.ShouldContain(DayOfWeek.Friday);
	}

	[Fact]
	public async Task If_GetAll_ThenReturnsAllFlags()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag1 = _fixture.CreateTestFlag("flag-1", FeatureFlagStatus.Enabled);
		flag1.Name = "Alpha Flag";
		var flag2 = _fixture.CreateTestFlag("flag-2", FeatureFlagStatus.Disabled);
		flag2.Name = "Beta Flag";
		var flag3 = _fixture.CreateTestFlag("flag-3", FeatureFlagStatus.Scheduled);
		flag3.Name = "Gamma Flag";

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);
		await _fixture.Repository.CreateAsync(flag3);

		// Act
		var result = await _fixture.Repository.GetAllAsync();

		// Assert
		result.Count.ShouldBe(3);
		// Should be ordered by name
		result[0].Name.ShouldBe("Alpha Flag");
		result[1].Name.ShouldBe("Beta Flag");
		result[2].Name.ShouldBe("Gamma Flag");
	}

	[Fact]
	public async Task If_GetAllWithNoFlags_ThenReturnsEmptyList()
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

public class When_FlagDoesNotExist : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public When_FlagDoesNotExist(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_Update_ThenThrowsInvalidOperationException()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("non-existent", FeatureFlagStatus.Enabled);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _fixture.Repository.UpdateAsync(flag));
		
		exception.Message.ShouldContain("Feature flag 'non-existent' not found");
	}

	[Fact]
	public async Task If_Delete_ThenReturnsFalse()
	{
		// Arrange
		await _fixture.ClearAllFlags();

		// Act
		var result = await _fixture.Repository.DeleteAsync("non-existent");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task If_Get_ThenReturnsNull()
	{
		// Act
		var result = await _fixture.Repository.GetAsync("non-existent-flag");

		// Assert
		result.ShouldBeNull();
	}
}

public class GetExpiringAsync_WithExpiringFlags : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public GetExpiringAsync_WithExpiringFlags(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsExpiringFlags()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var expiredFlag = _fixture.CreateTestFlag("expired-flag", FeatureFlagStatus.Enabled);
		expiredFlag.ExpirationDate = DateTime.UtcNow.AddDays(-1);
		expiredFlag.IsPermanent = false;

		var expiringFlag = _fixture.CreateTestFlag("expiring-flag", FeatureFlagStatus.Enabled);
		expiringFlag.ExpirationDate = DateTime.UtcNow.AddHours(1);
		expiringFlag.IsPermanent = false;

		var permanentFlag = _fixture.CreateTestFlag("permanent-flag", FeatureFlagStatus.Enabled);
		permanentFlag.ExpirationDate = DateTime.UtcNow.AddDays(-1);
		permanentFlag.IsPermanent = true;

		var futureFlag = _fixture.CreateTestFlag("future-flag", FeatureFlagStatus.Enabled);
		futureFlag.ExpirationDate = DateTime.UtcNow.AddDays(30);
		futureFlag.IsPermanent = false;

		await _fixture.Repository.CreateAsync(expiredFlag);
		await _fixture.Repository.CreateAsync(expiringFlag);
		await _fixture.Repository.CreateAsync(permanentFlag);
		await _fixture.Repository.CreateAsync(futureFlag);

		// Act
		var result = await _fixture.Repository.GetExpiringAsync(DateTime.UtcNow.AddHours(2));

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContain(f => f.Key == "expired-flag");
		result.ShouldContain(f => f.Key == "expiring-flag");
		result.ShouldNotContain(f => f.Key == "permanent-flag"); // Excluded because IsPermanent = true
		result.ShouldNotContain(f => f.Key == "future-flag"); // Excluded because expires after cutoff
	}

	[Fact]
	public async Task If_NoExpiringFlags_ThenReturnsEmptyList()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("future-flag", FeatureFlagStatus.Enabled);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(30);
		flag.IsPermanent = false;
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.GetExpiringAsync(DateTime.UtcNow);

		// Assert
		result.Count.ShouldBe(0);
	}
}

public class GetByTagsAsync_WithMatchingTags : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public GetByTagsAsync_WithMatchingTags(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsMatchingFlags()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag1 = _fixture.CreateTestFlag("platform-flag", FeatureFlagStatus.Enabled);
		flag1.Tags = new Dictionary<string, string>
		{
			{ "team", "platform" },
			{ "environment", "production" }
		};

		var flag2 = _fixture.CreateTestFlag("feature-flag", FeatureFlagStatus.Enabled);
		flag2.Tags = new Dictionary<string, string>
		{
			{ "team", "features" },
			{ "environment", "production" }
		};

		var flag3 = _fixture.CreateTestFlag("dev-flag", FeatureFlagStatus.Enabled);
		flag3.Tags = new Dictionary<string, string>
		{
			{ "team", "platform" },
			{ "environment", "development" }
		};

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);
		await _fixture.Repository.CreateAsync(flag3);

		// Act
		var result = await _fixture.Repository.GetByTagsAsync(new Dictionary<string, string>
		{
			{ "team", "platform" }
		});

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContain(f => f.Key == "platform-flag");
		result.ShouldContain(f => f.Key == "dev-flag");
		result.ShouldNotContain(f => f.Key == "feature-flag");
	}

	[Fact]
	public async Task If_MultipleTagsCriteria_ThenReturnsOnlyExactMatches()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag1 = _fixture.CreateTestFlag("exact-match", FeatureFlagStatus.Enabled);
		flag1.Tags = new Dictionary<string, string>
		{
			{ "team", "platform" },
			{ "environment", "production" }
		};

		var flag2 = _fixture.CreateTestFlag("partial-match", FeatureFlagStatus.Enabled);
		flag2.Tags = new Dictionary<string, string>
		{
			{ "team", "platform" },
			{ "environment", "development" }
		};

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);

		// Act
		var result = await _fixture.Repository.GetByTagsAsync(new Dictionary<string, string>
		{
			{ "team", "platform" },
			{ "environment", "production" }
		});

		// Assert
		result.Count.ShouldBe(1);
		result.ShouldContain(f => f.Key == "exact-match");
		result.ShouldNotContain(f => f.Key == "partial-match");
	}

	[Fact]
	public async Task If_NoMatchingTags_ThenReturnsEmptyList()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("no-match", FeatureFlagStatus.Enabled);
		flag.Tags = new Dictionary<string, string>
		{
			{ "team", "platform" }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.GetByTagsAsync(new Dictionary<string, string>
		{
			{ "team", "nonexistent" }
		});

		// Assert
		result.Count.ShouldBe(0);
	}
}

public class SqlServerFeatureFlagRepository_CancellationToken : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public SqlServerFeatureFlagRepository_CancellationToken(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_CancellationRequested_ThenOperationsCancelled()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("cancellation-test", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		using var cts = new CancellationTokenSource();
		cts.Cancel(); // Cancel immediately

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => _fixture.Repository.GetAsync("cancellation-test", cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => _fixture.Repository.GetAllAsync(cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => _fixture.Repository.CreateAsync(flag, cts.Token));
	}
}

public class SqlServerFeatureFlagRepository_DatabaseErrors : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public SqlServerFeatureFlagRepository_DatabaseErrors(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_DuplicateKey_ThenThrowsSqlException()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag1 = _fixture.CreateTestFlag("duplicate-key", FeatureFlagStatus.Enabled);
		var flag2 = _fixture.CreateTestFlag("duplicate-key", FeatureFlagStatus.Disabled);

		await _fixture.Repository.CreateAsync(flag1);

		// Act & Assert
		await Should.ThrowAsync<SqlException>(
			() => _fixture.Repository.CreateAsync(flag2));
	}

	[Fact]
	public async Task If_InvalidConnectionString_ThenThrowsSqlException()
	{
		// Arrange
		var logger = new Mock<ILogger<SqlServerFeatureFlagRepository>>();
		var invalidRepository = new SqlServerFeatureFlagRepository("invalid-connection-string", logger.Object);
		var flag = _fixture.CreateTestFlag("test", FeatureFlagStatus.Enabled);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => invalidRepository.CreateAsync(flag));
	}
}

public class SqlServerFeatureFlagRepositoryFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _container;
	public SqlServerFeatureFlagRepository Repository { get; private set; } = null!;
	private readonly ILogger<SqlServerFeatureFlagRepository> _logger;

	public SqlServerFeatureFlagRepositoryFixture()
	{
		_container = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithPassword("StrongP@ssw0rd!")
			.WithEnvironment("ACCEPT_EULA", "Y")
			.WithEnvironment("SA_PASSWORD", "StrongP@ssw0rd!")
			.WithPortBinding(1433, true)
			.Build();

		_logger = new Mock<ILogger<SqlServerFeatureFlagRepository>>().Object;
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();
		var connectionString = _container.GetConnectionString();
		
		// Create the repository after container is started
		await CreateDatabase(connectionString);
		await CreateTables(connectionString);
		
		// Initialize repository with the connection string
		Repository = new SqlServerFeatureFlagRepository(connectionString, _logger);
	}

	public async Task DisposeAsync()
	{
		await _container.DisposeAsync();
	}

	private async Task CreateDatabase(string connectionString)
	{
		// The testcontainer already creates the database, so we just need to ensure it's accessible
		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();
		// Database is ready
	}

	private async Task CreateTables(string connectionString)
	{
		const string createTableSql = @"
		CREATE TABLE feature_flags (
			[key] NVARCHAR(255) PRIMARY KEY,
			[name] NVARCHAR(500) NOT NULL,
			[description] NVARCHAR(MAX) NOT NULL,
			[status] INT NOT NULL,
			created_at DATETIME2 NOT NULL,
			updated_at DATETIME2 NOT NULL,
			created_by NVARCHAR(255) NOT NULL,
			updated_by NVARCHAR(255) NOT NULL,
			expiration_date DATETIME2 NULL,
			scheduled_enable_date DATETIME2 NULL,
			scheduled_disable_date DATETIME2 NULL,
			window_start_time TIME NULL,
			window_end_time TIME NULL,
			time_zone NVARCHAR(100) NULL,
			window_days NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			percentage_enabled INT NOT NULL DEFAULT 0,
			targeting_rules NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			enabled_users NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			disabled_users NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			variations NVARCHAR(MAX) NOT NULL DEFAULT '{}',
			default_variation NVARCHAR(255) NOT NULL DEFAULT 'off',
			tags NVARCHAR(MAX) NOT NULL DEFAULT '{}',
			is_permanent BIT NOT NULL DEFAULT 0
		);

		CREATE INDEX IX_feature_flags_status ON feature_flags([status]);
		CREATE INDEX IX_feature_flags_expiration_date ON feature_flags(expiration_date) WHERE expiration_date IS NOT NULL;
		CREATE INDEX IX_feature_flags_created_at ON feature_flags(created_at);
	";

	using var connection = new SqlConnection(connectionString);
	await connection.OpenAsync();
	using var command = new SqlCommand(createTableSql, connection);
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

	// Helper method to clear data between tests
	public async Task ClearAllFlags()
	{
		var connectionString = _container.GetConnectionString();
		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new SqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();
	}
}