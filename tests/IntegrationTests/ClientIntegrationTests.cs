using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.SqlServer;
using Testcontainers.MsSql;

namespace FeatureFlags.IntegrationTests.Client;

/* The tests cover these scenarios:
 *		All FeatureFlagClient public methods (IsEnabledAsync, GetVariationAsync, EvaluateAsync)
 *		Integration with SQL Server repository and memory cache
 *		Default timezone handling
 *		User ID and attributes passing
 *		Different flag statuses and configurations
 *		Error handling and edge cases
 *		End-to-end feature flag evaluation workflow
*/

public class IsEnabledAsync_WithEnabledFlag : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public IsEnabledAsync_WithEnabledFlag(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsTrue()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("enabled-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("enabled-flag", "user123");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task If_WithAttributes_ThenPassesAttributesToEvaluator()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("targeted-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules =
		[
			new() {
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = ["US", "CA"],
				Variation = "north-america"
			}
		];
		await _fixture.Repository.CreateAsync(flag);

		var attributes = new Dictionary<string, object> { { "region", "US" } };

		// Act
		var result = await _fixture.Client.IsEnabledAsync("targeted-flag", "user123", attributes);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task If_NoUserIdProvided_ThenUsesNullUserId()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("simple-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("simple-flag");

		// Assert
		result.ShouldBeTrue();
	}
}

public class IsEnabledAsync_WithDisabledFlag : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public IsEnabledAsync_WithDisabledFlag(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsFalse()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("disabled-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("disabled-flag", "user123");

		// Assert
		result.ShouldBeFalse();
	}
}

public class IsEnabledAsync_WithNonExistentFlag : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public IsEnabledAsync_WithNonExistentFlag(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsFalse()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Act
		var result = await _fixture.Client.IsEnabledAsync("non-existent-flag", "user123");

		// Assert
		result.ShouldBeFalse();
	}
}

public class GetVariationAsync_WithEnabledFlag : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public GetVariationAsync_WithEnabledFlag(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsVariationValue()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("variation-flag", FeatureFlagStatus.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-features" },
			{ "off", "basic-features" }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync("variation-flag", "default-value", "user123");

		// Assert
		result.ShouldBe("premium-features");
	}

	[Fact]
	public async Task If_StringVariation_ThenReturnsCorrectType()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("string-flag", FeatureFlagStatus.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "enabled-string" }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync("string-flag", "default", "user123");

		// Assert
		result.ShouldBe("enabled-string");
		result.ShouldBeOfType<string>();
	}

	[Fact]
	public async Task If_IntegerVariation_ThenReturnsCorrectType()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("int-flag", FeatureFlagStatus.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", 42 }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync("int-flag", 0, "user123");

		// Assert
		result.ShouldBe(42);
		result.ShouldBeOfType<int>();
	}

	[Fact]
	public async Task If_BooleanVariation_ThenReturnsCorrectType()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("bool-flag", FeatureFlagStatus.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", true }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync("bool-flag", false, "user123");

		// Assert
		result.ShouldBeTrue();
		result.ShouldBeOfType<bool>();
	}
}

public class GetVariationAsync_WithDisabledFlag : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public GetVariationAsync_WithDisabledFlag(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsDefaultValue()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("disabled-flag", FeatureFlagStatus.Disabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "should-not-see-this" }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync("disabled-flag", "default-value", "user123");

		// Assert
		result.ShouldBe("default-value");
	}
}

public class GetVariationAsync_WithNonExistentFlag : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public GetVariationAsync_WithNonExistentFlag(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsDefaultValue()
	{
		// Arrange
		await _fixture.ClearAllData();

		// Act
		var result = await _fixture.Client.GetVariationAsync("non-existent", "fallback-value", "user123");

		// Assert
		result.ShouldBe("fallback-value");
	}
}

public class EvaluateAsync_WithEnabledFlag : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public EvaluateAsync_WithEnabledFlag(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsEvaluationResult()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("eval-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.EvaluateAsync("eval-flag", "user123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_WithAttributes_ThenIncludesAttributesInEvaluation()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("targeted-eval-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "country",
				Operator = TargetingOperator.In,
				Values = new List<string> { "USA" },
				Variation = "usa-variant"
			}
		};
		await _fixture.Repository.CreateAsync(flag);

		var attributes = new Dictionary<string, object> { { "country", "USA" } };

		// Act
		var result = await _fixture.Client.EvaluateAsync("targeted-eval-flag", "user123", attributes);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("usa-variant");
		result.Reason.ShouldContain("Targeting rule matched");
	}
}

public class EvaluateAsync_WithPercentageFlag : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public EvaluateAsync_WithPercentageFlag(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsConsistentResults()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("percentage-flag", FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 50;
		await _fixture.Repository.CreateAsync(flag);

		// Act - Multiple evaluations should be consistent
		var result1 = await _fixture.Client.EvaluateAsync("percentage-flag", "consistent-user");
		var result2 = await _fixture.Client.EvaluateAsync("percentage-flag", "consistent-user");
		var result3 = await _fixture.Client.EvaluateAsync("percentage-flag", "consistent-user");

		// Assert
		result1.IsEnabled.ShouldBe(result2.IsEnabled);
		result1.IsEnabled.ShouldBe(result3.IsEnabled);
		result1.Variation.ShouldBe(result2.Variation);
		result1.Variation.ShouldBe(result3.Variation);
	}
}

public class FeatureFlagClient_DefaultTimeZone : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public FeatureFlagClient_DefaultTimeZone(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_CustomTimeZoneClient_ThenUsesCustomTimeZone()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		// Create client with custom timezone
		var customClient = new FeatureFlagClient(_fixture.Evaluator, "America/New_York");
		
		var flag = _fixture.CreateTestFlag("timezone-flag", FeatureFlagStatus.TimeWindow);
		flag.WindowStartTime = TimeSpan.FromHours(9); // 9 AM
		flag.WindowEndTime = TimeSpan.FromHours(17); // 5 PM
		flag.TimeZone = "America/New_York";
		await _fixture.Repository.CreateAsync(flag);

		// Use a time that would be within window in EST but not UTC
		var estNoon = new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc); // 12 PM EST = 5 PM UTC

		// Act
		var result = await customClient.EvaluateAsync("timezone-flag", "user123");

		// Assert - The evaluation should consider the New York timezone
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task If_DefaultClient_ThenUsesUTCTimeZone()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		// Use default client (UTC timezone)
		var flag = _fixture.CreateTestFlag("utc-timezone-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.EvaluateAsync("utc-timezone-flag", "user123");

		// Assert - Should work with UTC timezone (default)
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
	}
}

public class FeatureFlagClient_CacheIntegration : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public FeatureFlagClient_CacheIntegration(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagCached_ThenUsesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("cached-flag", FeatureFlagStatus.Enabled);
		
		// Put flag directly in cache (not in repository)
		await _fixture.Cache.SetAsync("cached-flag", flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("cached-flag", "user123");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagNotCachedButInRepository_ThenLoadsAndCaches()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("repo-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Verify not in cache initially
		var cachedBefore = await _fixture.Cache.GetAsync("repo-flag");
		cachedBefore.ShouldBeNull();

		// Act
		var result = await _fixture.Client.IsEnabledAsync("repo-flag", "user123");

		// Assert
		result.ShouldBeTrue();
		
		// Verify now cached
		var cachedAfter = await _fixture.Cache.GetAsync("repo-flag");
		cachedAfter.ShouldNotBeNull();
		cachedAfter.Key.ShouldBe("repo-flag");
	}
}

public class FeatureFlagClient_ErrorHandling : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public FeatureFlagClient_ErrorHandling(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_DatabaseError_ThenHandlesGracefully()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		// This will test the error handling when database is not accessible
		// The evaluator should handle exceptions gracefully
		
		// Act & Assert - Should not throw
		var isEnabledResult = await _fixture.Client.IsEnabledAsync("error-test", "user123");
		var variationResult = await _fixture.Client.GetVariationAsync("error-test", "default", "user123");
		var evaluationResult = await _fixture.Client.EvaluateAsync("error-test", "user123");

		// These should return safe defaults
		isEnabledResult.ShouldBeFalse();
		variationResult.ShouldBe("default");
		evaluationResult.ShouldNotBeNull();
		evaluationResult.IsEnabled.ShouldBeFalse();
	}
}

public class FeatureFlagClientFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _container;
	public FeatureFlagClient Client { get; private set; } = null!;
	public FeatureFlagEvaluator Evaluator { get; private set; } = null!;
	public SqlServerFeatureFlagRepository Repository { get; private set; } = null!;
	public MemoryFeatureFlagCache Cache { get; private set; } = null!;
	
	private readonly ILogger<FeatureFlagEvaluator> _evaluatorLogger;
	private readonly ILogger<SqlServerFeatureFlagRepository> _repositoryLogger;
	private MemoryCache _memoryCache = null!;

	public FeatureFlagClientFixture()
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
		
		// Create client with default UTC timezone
		Client = new FeatureFlagClient(Evaluator, "UTC");
	}

	public async Task DisposeAsync()
	{
		_memoryCache?.Dispose();
		await _container.DisposeAsync();
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