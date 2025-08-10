using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.SqlServer;
using Testcontainers.MsSql;

namespace FeatureFlags.IntegrationTests.Evaluator;

/* The tests cover these scenarios with MemoryFeatureFlagCache and SqlServerFeatureFlagRepository implementations:
 *		All feature flag statuses (Enabled, Disabled, Scheduled, TimeWindow, UserTargeted, Percentage)
 *		Cache-first evaluation strategy
 *		Repository fallback when cache misses
 *		User overrides (enabled/disabled users)
 *		Flag expiration handling
 *		Targeting rule evaluation
 *		Percentage rollout with consistent hashing
 *		Time-based evaluations with timezone handling
 *		Variation retrieval with type conversion
 *		Error handling and edge cases
 *		Cancellation token support
*/

public class EvaluateAsync_WithEnabledFlagV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("enabled-flag", FeatureFlagStatus.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("enabled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_FlagInCache_ThenUsesCache()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("cached-flag", FeatureFlagStatus.Enabled);
		
		// Only put in cache, not in repository
		await fixture.Cache.SetAsync("cached-flag", flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("cached-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}

	[Fact]
	public async Task If_FlagNotInCacheButInRepository_ThenCachesFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("repo-flag", FeatureFlagStatus.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("repo-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();

		// Verify flag is now cached
		var cachedFlag = await fixture.Cache.GetAsync("repo-flag");
		cachedFlag.ShouldNotBeNull();
		cachedFlag.Key.ShouldBe("repo-flag");
	}
}

public class EvaluateAsync_WithDisabledFlagV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("disabled-flag", FeatureFlagStatus.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("disabled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag disabled");
	}
}

public class EvaluateAsync_WithNonExistentFlagV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task ThenReturnsFlagNotFound()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("non-existent", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag not found, using default disabled flag");
	}
}

public class EvaluateAsync_WithExpiredFlagV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task ThenReturnsExpired()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("expired-flag", FeatureFlagStatus.Enabled);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(-1); // Expired yesterday
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("expired-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag expired");
	}
}

public class EvaluateAsync_WithUserOverridesV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_UserExplicitlyEnabled_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("override-flag", FeatureFlagStatus.Disabled);
		flag.EnabledUsers = ["user123", "user456"];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("override-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly enabled");
	}

	[Fact]
	public async Task If_UserExplicitlyDisabled_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("override-flag", FeatureFlagStatus.Enabled);
		flag.DisabledUsers = ["user123", "user456"];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("override-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("User explicitly disabled");
	}
}

public class EvaluateAsync_WithScheduledFlagV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_BeforeEnableDate_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("scheduled-flag", FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(1);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddDays(1);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("scheduled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_AfterEnableBeforeDisable_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("scheduled-flag", FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(-1);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddHours(1);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("scheduled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_AfterDisableDate_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("scheduled-flag", FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(-2);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddHours(-1);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("scheduled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Scheduled disable date passed");
	}
}

public class EvaluateAsync_WithTimeWindowFlagV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_WithinTimeWindow_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("time-window-flag", FeatureFlagStatus.TimeWindow);
		flag.WindowStartTime = TimeSpan.FromHours(9); // 9 AM
		flag.WindowEndTime = TimeSpan.FromHours(17); // 5 PM
		flag.TimeZone = "UTC";
		await fixture.Repository.CreateAsync(flag);

		// Use 12 PM UTC as evaluation time
		var noonUtc = DateTime.UtcNow.Date.AddHours(12);
		var context = new EvaluationContext(userId: "user123", evaluationTime: noonUtc, timeZone: "UTC");

		// Act
		var result = await fixture.Evaluator.Evaluate("time-window-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_OutsideTimeWindow_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("time-window-flag", FeatureFlagStatus.TimeWindow);
		flag.WindowStartTime = TimeSpan.FromHours(9); // 9 AM
		flag.WindowEndTime = TimeSpan.FromHours(17); // 5 PM
		flag.TimeZone = "UTC";
		await fixture.Repository.CreateAsync(flag);

		// Use 8 AM UTC as evaluation time (before window)
		var earlyMorning = DateTime.UtcNow.Date.AddHours(8);
		var context = new EvaluationContext(userId: "user123", evaluationTime: earlyMorning, timeZone: "UTC");

		// Act
		var result = await fixture.Evaluator.Evaluate("time-window-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Outside time window");
	}

	[Fact]
	public async Task If_OutsideAllowedDays_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("time-window-flag", FeatureFlagStatus.TimeWindow);
		flag.WindowStartTime = TimeSpan.FromHours(9);
		flag.WindowEndTime = TimeSpan.FromHours(17);
		flag.WindowDays = [DayOfWeek.Monday, DayOfWeek.Tuesday];
		flag.TimeZone = "UTC";
		await fixture.Repository.CreateAsync(flag);

		// Find a Wednesday and set evaluation time to noon
		var wednesday = DateTime.UtcNow.Date;
		while (wednesday.DayOfWeek != DayOfWeek.Wednesday)
		{
			wednesday = wednesday.AddDays(1);
		}
		wednesday = wednesday.AddHours(12); // Noon on Wednesday

		var context = new EvaluationContext(userId: "user123", evaluationTime: wednesday, timeZone: "UTC");

		// Act
		var result = await fixture.Evaluator.Evaluate("time-window-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Outside allowed days");
	}
}

public class EvaluateAsync_WithUserTargetedFlagV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_TargetingRuleMatches_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("targeted-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules =
		[
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = ["US", "CA"],
				Variation = "north-america"
			}
		];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123", attributes: new Dictionary<string, object> { { "region", "US" } });

		// Act
		var result = await fixture.Evaluator.Evaluate("targeted-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("north-america");
		result.Reason.ShouldContain("Targeting rule matched");
	}

	[Fact]
	public async Task If_NoTargetingRuleMatches_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("targeted-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules =
		[
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = ["US", "CA"],
				Variation = "north-america"
			}
		];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123", attributes: new Dictionary<string, object> { { "region", "EU" } });

		// Act
		var result = await fixture.Evaluator.Evaluate("targeted-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class EvaluateAsync_WithPercentageFlagV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_NoUserId_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("percentage-flag", FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 50;
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: null); // No UserId

		// Act
		var result = await fixture.Evaluator.Evaluate("percentage-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("User ID required for percentage rollout");
	}

	[Fact]
	public async Task If_SameUserIdAlwaysGivesSameResult()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("percentage-flag", FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 50;
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "consistent-user");

		// Act - Multiple evaluations
		var result1 = await fixture.Evaluator.Evaluate("percentage-flag", context);
		var result2 = await fixture.Evaluator.Evaluate("percentage-flag", context);
		var result3 = await fixture.Evaluator.Evaluate("percentage-flag", context);

		// Assert - All results should be the same
		result2.IsEnabled.ShouldBe(result1.IsEnabled);
		result3.IsEnabled.ShouldBe(result1.IsEnabled);
		result2.Variation.ShouldBe(result1.Variation);
		result3.Variation.ShouldBe(result1.Variation);
	}
}

public class GetVariationAsync_WithVariationsV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_FlagEnabledWithVariation_ThenReturnsVariationValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("variation-flag", FeatureFlagStatus.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-features" },
			{ "off", "basic-features" }
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("variation-flag", "default", context);

		// Assert
		result.ShouldBe("premium-features");
	}

	[Fact]
	public async Task If_FlagDisabled_ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("variation-flag", FeatureFlagStatus.Disabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-features" },
			{ "off", "basic-features" }
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("variation-flag", "default-value", context);

		// Assert
		result.ShouldBe("default-value");
	}

	[Fact]
	public async Task If_ComplexObjectVariation_ThenDeserializesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("config-flag", FeatureFlagStatus.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", new { MaxItems = 100, EnableAdvanced = true } }
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("config-flag", new { MaxItems = 10, EnableAdvanced = false }, context);

		// Assert
		result.ShouldNotBeNull();
		result.MaxItems.ShouldBe(100);
		result.EnableAdvanced.ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagNotFound_ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("non-existent", 42, context);

		// Assert
		result.ShouldBe(42);
	}
}

public class FeatureFlagEvaluator_CancellationTokenV2(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_CancellationRequested_ThenOperationsCancelled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("cancel-test", FeatureFlagStatus.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		using var cts = new CancellationTokenSource();
		cts.Cancel(); // Cancel immediately

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => fixture.Evaluator.Evaluate("cancel-test", context, cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => fixture.Evaluator.GetVariation("cancel-test", "default", context, cts.Token));
	}
}

public class FeatureFlagEvaluatorFixtureV2 : IAsyncLifetime
{
	private readonly MsSqlContainer _container;
	public FeatureFlagEvaluator Evaluator { get; private set; } = null!;
	public SqlServerFeatureFlagRepository Repository { get; private set; } = null!;
	public MemoryFeatureFlagCache Cache { get; private set; } = null!;

	private readonly ILogger<FeatureFlagEvaluator> _evaluatorLogger;
	private readonly ILogger<SqlServerFeatureFlagRepository> _repositoryLogger;
	private MemoryCache _memoryCache = null!;

	public FeatureFlagEvaluatorFixtureV2()
	{
		_container = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithPassword("StrongP@ssw0rd!")
			.WithEnvironment("ACCEPT_EULA", "Y")
			.WithEnvironment("SA_PASSWORD", "StrongP@ssw0rd!")
			.WithPortBinding(1433, true)
			.Build();

		_evaluatorLogger = new Mock<ILogger<FeatureFlagEvaluator>>().Object;
		_repositoryLogger = new Mock<ILogger<SqlServerFeatureFlagRepository>>().Object;
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();
		var connectionString = _container.GetConnectionString();

		// Create database tables
		await CreateTables(connectionString);

		// Initialize components
		Repository = new SqlServerFeatureFlagRepository(connectionString, _repositoryLogger);

		_memoryCache = new MemoryCache(new MemoryCacheOptions());
		Cache = new MemoryFeatureFlagCache(_memoryCache);

		Evaluator = new FeatureFlagEvaluator(Repository, Cache, _evaluatorLogger);
	}

	public async Task DisposeAsync()
	{
		await _container.DisposeAsync();
	}

	private static async Task CreateTables(string connectionString)
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

	public static FeatureFlag CreateTestFlag(string key, FeatureFlagStatus status)
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
			TargetingRules = [],
			EnabledUsers = [],
			DisabledUsers = [],
			Variations = [],
			Tags = [],
			WindowDays = [],
			PercentageEnabled = 0,
			IsPermanent = false
		};
	}

	// Helper method to clear all data between tests
	public async Task ClearAllData()
	{
		// Clear SQL Server
		var connectionString = _container.GetConnectionString();
		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new SqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();

		// Clear Memory Cache
		await Cache.ClearAsync();
	}
}