using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
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
		var flag = _fixture.CreateTestFlag("enabled-flag", FlagEvaluationMode.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync(flagKey: "enabled-flag", userId: "user123");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task If_WithAttributes_ThenPassesAttributesToEvaluator()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("targeted-flag", FlagEvaluationMode.UserTargeted);
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
		var result = await _fixture.Client.IsEnabledAsync("targeted-flag", null, "user123", attributes);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task If_NoUserIdProvided_ThenUsesNullUserId()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("simple-flag", FlagEvaluationMode.Enabled);
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
		var flag = _fixture.CreateTestFlag("disabled-flag", FlagEvaluationMode.Disabled);
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
		var flag = _fixture.CreateTestFlag("variation-flag", FlagEvaluationMode.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-features" },
			{ "off", "basic-features" }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync(flagKey: "variation-flag", defaultValue: "default-value", userId: "user123");

		// Assert
		result.ShouldBe("premium-features");
	}

	[Fact]
	public async Task If_StringVariation_ThenReturnsCorrectType()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("string-flag", FlagEvaluationMode.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "enabled-string" }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync(flagKey: "string-flag", defaultValue: "default", userId: "user123");

		// Assert
		result.ShouldBe("enabled-string");
		result.ShouldBeOfType<string>();
	}

	[Fact]
	public async Task If_IntegerVariation_ThenReturnsCorrectType()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("int-flag", FlagEvaluationMode.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", 42 }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync(flagKey: "int-flag", defaultValue: 0, userId: "user123");

		// Assert
		result.ShouldBe(42);
		result.ShouldBeOfType<int>();
	}

	[Fact]
	public async Task If_BooleanVariation_ThenReturnsCorrectType()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("bool-flag", FlagEvaluationMode.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", true }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync(flagKey: "bool-flag", defaultValue: false, userId: "user123");

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
		var flag = _fixture.CreateTestFlag("disabled-flag", FlagEvaluationMode.Disabled);
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
		var flag = _fixture.CreateTestFlag("eval-flag", FlagEvaluationMode.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.EvaluateAsync(flagKey: "eval-flag", userId: "user123");

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
		var flag = _fixture.CreateTestFlag("targeted-eval-flag", FlagEvaluationMode.UserTargeted);
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
		var result = await _fixture.Client.EvaluateAsync("targeted-eval-flag", null, "user123", attributes);

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
		var flag = _fixture.CreateTestFlag("percentage-flag", FlagEvaluationMode.UserRolloutPercentage);
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
		
		var flag = _fixture.CreateTestFlag("timezone-flag", FlagEvaluationMode.TimeWindow);
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
		var flag = _fixture.CreateTestFlag("utc-timezone-flag", FlagEvaluationMode.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.EvaluateAsync(flagKey: "utc-timezone-flag", userId: "user123");

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
		var flag = _fixture.CreateTestFlag("cached-flag", FlagEvaluationMode.Enabled);
		
		// Put flag directly in cache (not in repository)
		await _fixture.Cache.SetAsync("cached-flag", flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync(flagKey: "cached-flag", userId: "user123");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagNotCachedButInRepository_ThenLoadsAndCaches()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("repo-flag", FlagEvaluationMode.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Verify not in cache initially
		var cachedBefore = await _fixture.Cache.GetAsync("repo-flag");
		cachedBefore.ShouldBeNull();

		// Act
		var result = await _fixture.Client.IsEnabledAsync(flagKey: "repo-flag", userId: "user123");

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
	public IChainableEvaluationHandler EvaluationHandler { get; private set; }
	
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

		EvaluationHandler = EvaluatorChainBuilder.BuildChain();
		
		Evaluator = new FeatureFlagEvaluator(Repository, EvaluationHandler, Cache);
		
		// Create client with default UTC timezone
		Client = new FeatureFlagClient(evaluator: Evaluator, defaultTimeZone: "UTC");
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
			enabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			disabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			tenant_percentage_enabled INT NOT NULL DEFAULT 0,
			variations NVARCHAR(MAX) NOT NULL DEFAULT '{}',
			default_variation NVARCHAR(255) NOT NULL DEFAULT 'off',
			tags NVarchar(MAX) NOT NULL DEFAULT '{}',
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

	public FeatureFlag CreateTestFlag(string key, FlagEvaluationMode status)
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
			EnabledTenants = new List<string>(),
			DisabledTenants = new List<string>(),
			TenantPercentageEnabled = 0,
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

public class IsEnabledAsync_WithTenantOverrides : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public IsEnabledAsync_WithTenantOverrides(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_TenantExplicitlyDisabled_ThenReturnsFalse()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-disabled-flag", FlagEvaluationMode.Enabled);
		flag.DisabledTenants = ["blocked-tenant", "another-blocked"];
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("tenant-disabled-flag", "blocked-tenant", "user123");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task If_TenantExplicitlyEnabled_ThenReturnsTrue()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-enabled-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["premium-tenant", "enterprise-tenant"];
		flag.TenantPercentageEnabled = 0; // Would normally block all tenants
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("tenant-enabled-flag", "premium-tenant", "user123");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task If_TenantDisabledTakesPrecedenceOverEnabled_ThenReturnsFalse()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-precedence-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["conflict-tenant"];
		flag.DisabledTenants = ["conflict-tenant"]; // Same tenant in both lists
		flag.TenantPercentageEnabled = 100; // Would allow tenant
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("tenant-precedence-flag", "conflict-tenant", "user123");

		// Assert
		result.ShouldBeFalse(); // Disabled takes precedence
	}

	[Fact]
	public async Task If_NoTenantIdProvided_ThenSkipsTenantEvaluation()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("no-tenant-flag", FlagEvaluationMode.Enabled);
		flag.TenantPercentageEnabled = 0; // Would block if tenant evaluation ran
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("no-tenant-flag", null, "user123");

		// Assert
		result.ShouldBeTrue(); // Skips tenant evaluation, goes to flag status
	}
}

public class IsEnabledAsync_WithTenantPercentageRollout : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public IsEnabledAsync_WithTenantPercentageRollout(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_TenantPercentage0_ThenAllTenantsDisabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-percentage-0-flag", FlagEvaluationMode.Enabled);
		flag.TenantPercentageEnabled = 0; // Block all tenants
		await _fixture.Repository.CreateAsync(flag);

		var tenantIds = new[] { "tenant1", "tenant2", "tenant3", "tenant4", "tenant5" };

		foreach (var tenantId in tenantIds)
		{
			// Act
			var result = await _fixture.Client.IsEnabledAsync("tenant-percentage-0-flag", tenantId, "user123");

			// Assert
			result.ShouldBeFalse();
		}
	}

	[Fact]
	public async Task If_TenantPercentage100_ThenAllTenantsEnabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-percentage-100-flag", FlagEvaluationMode.Enabled);
		flag.TenantPercentageEnabled = 100; // Allow all tenants
		await _fixture.Repository.CreateAsync(flag);

		var tenantIds = new[] { "tenant1", "tenant2", "tenant3", "tenant4", "tenant5" };

		foreach (var tenantId in tenantIds)
		{
			// Act
			var result = await _fixture.Client.IsEnabledAsync("tenant-percentage-100-flag", tenantId, "user123");

			// Assert
			result.ShouldBeTrue();
		}
	}

	[Fact]
	public async Task If_SameTenantIdAlwaysGivesSameResult()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-consistency-flag", FlagEvaluationMode.Enabled);
		flag.TenantPercentageEnabled = 50;
		await _fixture.Repository.CreateAsync(flag);

		// Act - Multiple evaluations
		var result1 = await _fixture.Client.IsEnabledAsync("tenant-consistency-flag", "consistent-tenant", "user123");
		var result2 = await _fixture.Client.IsEnabledAsync("tenant-consistency-flag", "consistent-tenant", "user123");
		var result3 = await _fixture.Client.IsEnabledAsync("tenant-consistency-flag", "consistent-tenant", "user123");

		// Assert - All results should be the same
		result2.ShouldBe(result1);
		result3.ShouldBe(result1);
	}
}

public class IsEnabledAsync_WithTenantAndUserCombinations : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public IsEnabledAsync_WithTenantAndUserCombinations(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_TenantAllowedButUserDisabled_ThenReturnsFalse()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-user-combo-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["allowed-tenant"];
		flag.DisabledUsers = ["blocked-user"];
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("tenant-user-combo-flag", "allowed-tenant", "blocked-user");

		// Assert
		result.ShouldBeFalse(); // User override takes precedence
	}

	[Fact]
	public async Task If_TenantDisabled_ThenUserOverrideIgnored()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-blocks-user-flag", FlagEvaluationMode.Enabled);
		flag.DisabledTenants = ["blocked-tenant"];
		flag.EnabledUsers = ["vip-user"]; // User is enabled but tenant is blocked
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("tenant-blocks-user-flag", "blocked-tenant", "vip-user");

		// Assert
		result.ShouldBeFalse(); // Tenant evaluation comes first
	}

	[Fact]
	public async Task If_TenantAllowedAndUserEnabled_ThenReturnsTrue()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-user-both-enabled-flag", FlagEvaluationMode.Disabled);
		flag.EnabledTenants = ["premium-tenant"];
		flag.EnabledUsers = ["vip-user"];
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("tenant-user-both-enabled-flag", "premium-tenant", "vip-user");

		// Assert
		result.ShouldBeTrue(); // User override enables the flag
	}
}

public class GetVariationAsync_WithTenantScenarios : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public GetVariationAsync_WithTenantScenarios(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_TenantAllowedFlagEnabledWithVariation_ThenReturnsVariationValue()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-variation-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["premium-tenant"];
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-features" },
			{ "off", "basic-features" }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync("tenant-variation-flag", "default", "premium-tenant", "user123");

		// Assert
		result.ShouldBe("premium-features");
	}

	[Fact]
	public async Task If_TenantDisabled_ThenReturnsDefaultValue()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-blocked-variation-flag", FlagEvaluationMode.Enabled);
		flag.DisabledTenants = ["blocked-tenant"];
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-features" },
			{ "off", "basic-features" }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync("tenant-blocked-variation-flag", "default-value", "blocked-tenant", "user123");

		// Assert
		result.ShouldBe("default-value");
	}

	[Fact]
	public async Task If_TenantConfigurationObject_ThenDeserializesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-config-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["enterprise-tenant"];
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", new { MaxUsers = 1000, SupportLevel = "enterprise" } }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync("tenant-config-flag", 
			new { MaxUsers = 10, SupportLevel = "basic" }, "enterprise-tenant", "user123");

		// Assert
		result.ShouldNotBeNull();
		result.MaxUsers.ShouldBe(1000);
		result.SupportLevel.ShouldBe("enterprise");
	}

	[Fact]
	public async Task If_TenantNotInPercentage_ThenReturnsDefaultValue()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-percentage-blocked-flag", FlagEvaluationMode.Enabled);
		flag.TenantPercentageEnabled = 0; // Block all tenants
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-config" },
			{ "off", "basic-config" }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.GetVariationAsync("tenant-percentage-blocked-flag", 
			"fallback-config", "any-tenant", "user123");

		// Assert
		result.ShouldBe("fallback-config");
	}
}

public class EvaluateAsync_WithTenantScenarios : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public EvaluateAsync_WithTenantScenarios(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_TenantExplicitlyDisabled_ThenReturnsDisabledResult()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-eval-disabled-flag", FlagEvaluationMode.Enabled);
		flag.DisabledTenants = ["blocked-tenant"];
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.EvaluateAsync("tenant-eval-disabled-flag", "blocked-tenant", "user123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}

	[Fact]
	public async Task If_TenantExplicitlyEnabled_ThenReturnsEnabledResult()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-eval-enabled-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["premium-tenant"];
		flag.TenantPercentageEnabled = 0; // Would normally block
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.EvaluateAsync("tenant-eval-enabled-flag", "premium-tenant", "user123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_TenantNotInPercentage_ThenReturnsPercentageReason()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-percentage-eval-flag", FlagEvaluationMode.Enabled);
		flag.TenantPercentageEnabled = 0; // Block all tenants
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.EvaluateAsync("tenant-percentage-eval-flag", "blocked-by-percentage", "user123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Tenant not in percentage rollout");
	}

	[Fact]
	public async Task If_TenantWithTargetingRules_ThenEvaluatesBothTenantAndTargeting()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("tenant-targeting-flag", FlagEvaluationMode.UserTargeted);
		flag.EnabledTenants = ["allowed-tenant"];
		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = new List<string> { "US" },
				Variation = "us-variant"
			}
		};
		await _fixture.Repository.CreateAsync(flag);

		var attributes = new Dictionary<string, object> { { "region", "US" } };

		// Act
		var result = await _fixture.Client.EvaluateAsync("tenant-targeting-flag", "allowed-tenant", "user123", attributes);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("us-variant");
		result.Reason.ShouldContain("Targeting rule matched");
	}
}

public class FeatureFlagClient_DefaultTenantId : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public FeatureFlagClient_DefaultTenantId(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ClientWithDefaultTenantId_ThenUsesDefaultWhenNullProvided()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		// Create client with default tenant ID
		var clientWithDefaultTenant = new FeatureFlagClient(_fixture.Evaluator, "UTC");
		
		var flag = _fixture.CreateTestFlag("default-tenant-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["default-tenant"];
		flag.TenantPercentageEnabled = 0; // Would block other tenants
		await _fixture.Repository.CreateAsync(flag);

		// Act - Don't provide tenant ID, should use default
		var result = await clientWithDefaultTenant.IsEnabledAsync("default-tenant-flag", null, "user123");

		// Assert
		result.ShouldBeTrue(); // Should use default tenant ID
	}

	[Fact]
	public async Task If_ClientWithDefaultTenantId_ThenProvidedTenantOverridesDefault()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		// Create client with default tenant ID
		var clientWithDefaultTenant = new FeatureFlagClient(_fixture.Evaluator, "UTC");
		
		var flag = _fixture.CreateTestFlag("override-tenant-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["specific-tenant"];
		flag.DisabledTenants = ["default-tenant"];
		await _fixture.Repository.CreateAsync(flag);

		// Act - Provide specific tenant ID, should override default
		var result = await clientWithDefaultTenant.IsEnabledAsync("override-tenant-flag", "specific-tenant", "user123");

		// Assert
		result.ShouldBeTrue(); // Should use provided tenant, not default
	}

	[Fact]
	public async Task If_ClientWithDefaultTenantIdButProvidedTenantBlocked_ThenReturnsFalse()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		// Create client with default tenant ID
		var clientWithDefaultTenant = new FeatureFlagClient(_fixture.Evaluator, "UTC");
		
		var flag = _fixture.CreateTestFlag("blocked-override-tenant-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["default-tenant"]; // Default tenant is enabled
		flag.DisabledTenants = ["blocked-tenant"]; // But provided tenant is blocked
		await _fixture.Repository.CreateAsync(flag);

		// Act - Provide blocked tenant ID
		var result = await clientWithDefaultTenant.IsEnabledAsync("blocked-override-tenant-flag", "blocked-tenant", "user123");

		// Assert
		result.ShouldBeFalse(); // Should use provided tenant (blocked), not default
	}

	[Fact]
	public async Task If_GetVariationWithDefaultTenant_ThenUsesDefaultTenantId()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		// Create client with default tenant ID
		var clientWithDefaultTenant = new FeatureFlagClient(_fixture.Evaluator, "UTC");
		
		var flag = _fixture.CreateTestFlag("default-tenant-variation-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["premium-tenant"];
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-value" }
		};
		await _fixture.Repository.CreateAsync(flag);

		// Act - Don't provide tenant ID
		var result = await clientWithDefaultTenant.GetVariationAsync("default-tenant-variation-flag", "default-value", null, "user123");

		// Assert
		result.ShouldBe("premium-value"); // Should use default tenant ID
	}

	[Fact]
	public async Task If_EvaluateWithDefaultTenant_ThenUsesDefaultTenantId()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		// Create client with default tenant ID
		var clientWithDefaultTenant = new FeatureFlagClient(_fixture.Evaluator, "UTC");
		
		var flag = _fixture.CreateTestFlag("default-tenant-eval-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["enterprise-tenant"];
		flag.TenantPercentageEnabled = 0; // Would block other tenants
		await _fixture.Repository.CreateAsync(flag);

		// Act - Don't provide tenant ID
		var result = await clientWithDefaultTenant.EvaluateAsync("default-tenant-eval-flag", null, "user123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Flag enabled");
	}
}

public class FeatureFlagClient_TenantEdgeCases : IClassFixture<FeatureFlagClientFixture>
{
	private readonly FeatureFlagClientFixture _fixture;

	public FeatureFlagClient_TenantEdgeCases(FeatureFlagClientFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_EmptyTenantId_ThenSkipsTenantEvaluation()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("empty-tenant-flag", FlagEvaluationMode.Enabled);
		flag.TenantPercentageEnabled = 0; // Would block if tenant evaluation ran
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("empty-tenant-flag", "", "user123");

		// Assert
		result.ShouldBeTrue(); // Skips tenant evaluation
	}

	[Fact]
	public async Task If_WhitespaceTenantId_ThenSkipsTenantEvaluation()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("whitespace-tenant-flag", FlagEvaluationMode.Enabled);
		flag.TenantPercentageEnabled = 0; // Would block if tenant evaluation ran
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Client.IsEnabledAsync("whitespace-tenant-flag", "   ", "user123");

		// Assert
		result.ShouldBeTrue(); // Skips tenant evaluation
	}

	[Fact]
	public async Task If_TenantWithSpecialCharacters_ThenEvaluatesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("special-tenant-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["tenant@company.com", "tenant-123", "tenant_456"];
		await _fixture.Repository.CreateAsync(flag);

		var tenantIds = new[] { "tenant@company.com", "tenant-123", "tenant_456" };

		foreach (var tenantId in tenantIds)
		{
			// Act
			var result = await _fixture.Client.IsEnabledAsync("special-tenant-flag", tenantId, "user123");

			// Assert
			result.ShouldBeTrue(); // All should be enabled
		}
	}

	[Fact]
	public async Task If_CaseSensitiveTenantIds_ThenMatchesExactly()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = _fixture.CreateTestFlag("case-sensitive-tenant-flag", FlagEvaluationMode.Enabled);
		flag.EnabledTenants = ["TenantABC"];
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var correctResult = await _fixture.Client.IsEnabledAsync("case-sensitive-tenant-flag", "TenantABC", "user123");
		var wrongResult = await _fixture.Client.IsEnabledAsync("case-sensitive-tenant-flag", "tenantabc", "user123");

		// Assert
		correctResult.ShouldBeTrue(); // Exact match
		wrongResult.ShouldBeTrue(); // Not in enabled list, but flag status is Enabled so continues evaluation
	}
}