using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Redis;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace FeatureFlags.IntegrationTests.Redis;

/* The tests cover these scenarios:
 *		Cache operations (Get, Set, Remove, Clear)
 *		Complex data serialization/deserialization
 *		Key prefix handling
 *		Expiration functionality
 *		Error handling and edge cases
 *		Cancellation token support
 *		Redis-specific error scenarios
*/
public class SetAsync_WithValidFlag : IClassFixture<RedisFeatureFlagCacheFixture>
{
	private readonly RedisFeatureFlagCacheFixture _fixture;

	public SetAsync_WithValidFlag(RedisFeatureFlagCacheFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenStoresFlag()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("cache-test", FeatureFlagStatus.Enabled);

		// Act
		await _fixture.Cache.SetAsync("cache-test", flag);

		// Assert
		var retrieved = await _fixture.Cache.GetAsync("cache-test");
		retrieved.ShouldNotBeNull();
		retrieved.Key.ShouldBe("cache-test");
		retrieved.Status.ShouldBe(FeatureFlagStatus.Enabled);
	}

	[Fact]
	public async Task If_FlagWithComplexData_ThenStoresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("complex-flag", FeatureFlagStatus.UserTargeted);
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
		flag.Tags = new Dictionary<string, string>
		{
			{ "team", "platform" },
			{ "environment", "production" }
		};

		// Act
		await _fixture.Cache.SetAsync("complex-flag", flag);

		// Assert
		var retrieved = await _fixture.Cache.GetAsync("complex-flag");
		retrieved.ShouldNotBeNull();
		retrieved.TargetingRules.Count.ShouldBe(1);
		retrieved.TargetingRules[0].Attribute.ShouldBe("region");
		retrieved.TargetingRules[0].Values.ShouldContain("US");
		retrieved.EnabledUsers.ShouldContain("admin1");
		retrieved.Tags.ShouldContainKeyAndValue("team", "platform");
	}

	[Fact]
	public async Task If_FlagWithExpiration_ThenExpiresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("expiring-flag", FeatureFlagStatus.Enabled);
		var expiration = TimeSpan.FromSeconds(2);

		// Act
		await _fixture.Cache.SetAsync("expiring-flag", flag, expiration);

		// Assert - Initially should exist
		var retrieved = await _fixture.Cache.GetAsync("expiring-flag");
		retrieved.ShouldNotBeNull();

		// Wait for expiration
		await Task.Delay(TimeSpan.FromSeconds(3));

		// Should be expired now
		var expiredRetrieved = await _fixture.Cache.GetAsync("expiring-flag");
		expiredRetrieved.ShouldBeNull();
	}

	[Fact]
	public async Task If_UpdateExistingFlag_ThenOverwritesData()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var originalFlag = _fixture.CreateTestFlag("update-flag", FeatureFlagStatus.Disabled);
		await _fixture.Cache.SetAsync("update-flag", originalFlag);

		var updatedFlag = _fixture.CreateTestFlag("update-flag", FeatureFlagStatus.Enabled);
		updatedFlag.Name = "Updated Name";
		updatedFlag.Description = "Updated Description";

		// Act
		await _fixture.Cache.SetAsync("update-flag", updatedFlag);

		// Assert
		var retrieved = await _fixture.Cache.GetAsync("update-flag");
		retrieved.ShouldNotBeNull();
		retrieved.Status.ShouldBe(FeatureFlagStatus.Enabled);
		retrieved.Name.ShouldBe("Updated Name");
		retrieved.Description.ShouldBe("Updated Description");
	}
}

public class GetAsync_WhenFlagExists : IClassFixture<RedisFeatureFlagCacheFixture>
{
	private readonly RedisFeatureFlagCacheFixture _fixture;

	public GetAsync_WhenFlagExists(RedisFeatureFlagCacheFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsFlag()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("get-test", FeatureFlagStatus.Enabled);
		await _fixture.Cache.SetAsync("get-test", flag);

		// Act
		var result = await _fixture.Cache.GetAsync("get-test");

		// Assert
		result.ShouldNotBeNull();
		result.Key.ShouldBe("get-test");
		result.Status.ShouldBe(FeatureFlagStatus.Enabled);
		result.Name.ShouldBe(flag.Name);
		result.CreatedBy.ShouldBe(flag.CreatedBy);
	}

	[Fact]
	public async Task If_FlagWithTimeData_ThenDeserializesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("time-flag", FeatureFlagStatus.TimeWindow);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(30);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(1);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddDays(7);
		flag.WindowStartTime = TimeSpan.FromHours(9);
		flag.WindowEndTime = TimeSpan.FromHours(17);
		flag.TimeZone = "America/New_York";
		flag.WindowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday };

		await _fixture.Cache.SetAsync("time-flag", flag);

		// Act
		var result = await _fixture.Cache.GetAsync("time-flag");

		// Assert
		result.ShouldNotBeNull();
		result.ExpirationDate.ShouldNotBeNull();
		result.ExpirationDate.Value.ShouldBeInRange(flag.ExpirationDate.Value.AddSeconds(-1), flag.ExpirationDate.Value.AddSeconds(1));
		result.ScheduledEnableDate.ShouldNotBeNull();
		result.ScheduledEnableDate.Value.ShouldBeInRange(flag.ScheduledEnableDate.Value.AddSeconds(-1), flag.ScheduledEnableDate.Value.AddSeconds(1));
		result.WindowStartTime.ShouldBe(flag.WindowStartTime);
		result.WindowEndTime.ShouldBe(flag.WindowEndTime);
		result.TimeZone.ShouldBe("America/New_York");
		result.WindowDays.ShouldContain(DayOfWeek.Monday);
		result.WindowDays.ShouldContain(DayOfWeek.Friday);
	}

	[Fact]
	public async Task If_FlagWithPercentageData_ThenDeserializesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("percentage-flag", FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 75;

		await _fixture.Cache.SetAsync("percentage-flag", flag);

		// Act
		var result = await _fixture.Cache.GetAsync("percentage-flag");

		// Assert
		result.ShouldNotBeNull();
		result.PercentageEnabled.ShouldBe(75);
	}
}

public class GetAsync_WhenFlagDoesNotExist : IClassFixture<RedisFeatureFlagCacheFixture>
{
	private readonly RedisFeatureFlagCacheFixture _fixture;

	public GetAsync_WhenFlagDoesNotExist(RedisFeatureFlagCacheFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenReturnsNull()
	{
		// Arrange
		await _fixture.ClearAllFlags();

		// Act
		var result = await _fixture.Cache.GetAsync("non-existent-flag");

		// Assert
		result.ShouldBeNull();
	}
}

public class RemoveAsync_WhenFlagExists : IClassFixture<RedisFeatureFlagCacheFixture>
{
	private readonly RedisFeatureFlagCacheFixture _fixture;

	public RemoveAsync_WhenFlagExists(RedisFeatureFlagCacheFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenRemovesFlag()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("remove-test", FeatureFlagStatus.Enabled);
		await _fixture.Cache.SetAsync("remove-test", flag);

		// Verify it exists first
		var beforeRemove = await _fixture.Cache.GetAsync("remove-test");
		beforeRemove.ShouldNotBeNull();

		// Act
		await _fixture.Cache.RemoveAsync("remove-test");

		// Assert
		var afterRemove = await _fixture.Cache.GetAsync("remove-test");
		afterRemove.ShouldBeNull();
	}

	[Fact]
	public async Task If_RemoveNonExistentFlag_ThenDoesNotThrow()
	{
		// Arrange
		await _fixture.ClearAllFlags();

		// Act & Assert - Should not throw
		await _fixture.Cache.RemoveAsync("non-existent-flag");
	}
}

public class ClearAsync_WithMultipleFlags : IClassFixture<RedisFeatureFlagCacheFixture>
{
	private readonly RedisFeatureFlagCacheFixture _fixture;

	public ClearAsync_WithMultipleFlags(RedisFeatureFlagCacheFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenRemovesAllFlags()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag1 = _fixture.CreateTestFlag("flag-1", FeatureFlagStatus.Enabled);
		var flag2 = _fixture.CreateTestFlag("flag-2", FeatureFlagStatus.Disabled);
		var flag3 = _fixture.CreateTestFlag("flag-3", FeatureFlagStatus.Percentage);

		await _fixture.Cache.SetAsync("flag-1", flag1);
		await _fixture.Cache.SetAsync("flag-2", flag2);
		await _fixture.Cache.SetAsync("flag-3", flag3);

		// Verify they exist
		(await _fixture.Cache.GetAsync("flag-1")).ShouldNotBeNull();
		(await _fixture.Cache.GetAsync("flag-2")).ShouldNotBeNull();
		(await _fixture.Cache.GetAsync("flag-3")).ShouldNotBeNull();

		// Act
		await _fixture.Cache.ClearAsync();

		// Assert
		(await _fixture.Cache.GetAsync("flag-1")).ShouldBeNull();
		(await _fixture.Cache.GetAsync("flag-2")).ShouldBeNull();
		(await _fixture.Cache.GetAsync("flag-3")).ShouldBeNull();
	}

	[Fact]
	public async Task If_NoFlags_ThenDoesNotThrow()
	{
		// Arrange
		await _fixture.ClearAllFlags();

		// Act & Assert - Should not throw
		await _fixture.Cache.ClearAsync();
	}

	[Fact]
	public async Task If_OnlyFeatureFlagKeys_ThenDoesNotAffectOtherKeys()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		
		// Set a feature flag
		var flag = _fixture.CreateTestFlag("test-flag", FeatureFlagStatus.Enabled);
		await _fixture.Cache.SetAsync("test-flag", flag);

		// Set a non-feature flag key directly in Redis
		var database = _fixture.GetDatabase();
		await database.StringSetAsync("other:key", "some-value");

		// Act
		await _fixture.Cache.ClearAsync();

		// Assert
		(await _fixture.Cache.GetAsync("test-flag")).ShouldBeNull(); // Feature flag should be removed
		(await database.StringGetAsync("other:key")).HasValue.ShouldBeTrue(); // Other key should remain
	}
}

public class RedisFeatureFlagCache_KeyPrefix : IClassFixture<RedisFeatureFlagCacheFixture>
{
	private readonly RedisFeatureFlagCacheFixture _fixture;

	public RedisFeatureFlagCache_KeyPrefix(RedisFeatureFlagCacheFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_SetAndGet_ThenUsesCorrectKeyPrefix()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("prefix-test", FeatureFlagStatus.Enabled);

		// Act
		await _fixture.Cache.SetAsync("prefix-test", flag);

		// Assert
		var database = _fixture.GetDatabase();
		var directValue = await database.StringGetAsync("ff:prefix-test");
		directValue.HasValue.ShouldBeTrue();

		// Verify we can retrieve through cache
		var retrieved = await _fixture.Cache.GetAsync("prefix-test");
		retrieved.ShouldNotBeNull();
	}
}

public class RedisFeatureFlagCache_CancellationToken : IClassFixture<RedisFeatureFlagCacheFixture>
{
	private readonly RedisFeatureFlagCacheFixture _fixture;

	public RedisFeatureFlagCache_CancellationToken(RedisFeatureFlagCacheFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_CancellationRequested_ThenOperationsCancelled()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("cancellation-test", FeatureFlagStatus.Enabled);
		await _fixture.Cache.SetAsync("cancellation-test", flag);

		using var cts = new CancellationTokenSource();
		cts.Cancel(); // Cancel immediately

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => _fixture.Cache.GetAsync("cancellation-test", cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => _fixture.Cache.SetAsync("new-flag", flag, null, cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => _fixture.Cache.RemoveAsync("cancellation-test", cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => _fixture.Cache.ClearAsync(cts.Token));
	}
}

public class RedisFeatureFlagCache_ErrorHandling : IClassFixture<RedisFeatureFlagCacheFixture>
{
	private readonly RedisFeatureFlagCacheFixture _fixture;

	public RedisFeatureFlagCache_ErrorHandling(RedisFeatureFlagCacheFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public void If_InvalidConnectionMultiplexer_ThenThrowsRedisException()
	{
		// Act & Assert
		Should.Throw<RedisConnectionException>(
			() => ConnectionMultiplexer.Connect("invalid-connection-string"));
	}

	[Fact]
	public async Task If_CorruptedData_ThenGetReturnsNull()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var database = _fixture.GetDatabase();
		
		// Set corrupted JSON data directly
		await database.StringSetAsync("ff:corrupted-flag", "invalid-json-data");

		// Act
		var result = await _fixture.Cache.GetAsync("corrupted-flag");

		// Assert
		result.ShouldBeNull(); // Should return null instead of throwing
	}
}

public class RedisFeatureFlagCacheFixture : IAsyncLifetime
{
	private readonly RedisContainer _container;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	private IConnectionMultiplexer _connectionMultiplexer = null!;
	private readonly ILogger<RedisFeatureFlagCache> _logger;

	public RedisFeatureFlagCacheFixture()
	{
		_container = new RedisBuilder()
			.WithImage("redis:7-alpine")
			.WithPortBinding(6379, true)
			.Build();

		_logger = new Mock<ILogger<RedisFeatureFlagCache>>().Object;
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();
		var connectionString = _container.GetConnectionString();
		
		// Create connection multiplexer
		_connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(connectionString);
		
		// Initialize cache
		Cache = new RedisFeatureFlagCache(_connectionMultiplexer, _logger);
	}

	public async Task DisposeAsync()
	{
		_connectionMultiplexer?.Dispose();
		await _container.DisposeAsync();
	}

	public IDatabase GetDatabase()
	{
		return _connectionMultiplexer.GetDatabase();
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

	// Helper method to clear all feature flags between tests
	public async Task ClearAllFlags()
	{
		await Cache.ClearAsync();
	}
}