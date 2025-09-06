using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Redis;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace FeatureFlags.IntegrationTests;

/* The tests cover these scenarios:
 *		Cache operations (Get, Set, Remove, Clear)
 *		Complex data serialization/deserialization
 *		Key prefix handling
 *		Expiration functionality
 *		Error handling and edge cases
 *		Cancellation token support
 *		Redis-specific error scenarios
*/
public class SetAsync_WithValidFlag(RedisFeatureFlagCacheFixture fixture) : IClassFixture<RedisFeatureFlagCacheFixture>
{
	[Fact]
	public async Task ThenStoresFlag()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = RedisFeatureFlagCacheFixture.CreateTestFlag("cache-test", FlagEvaluationMode.Enabled);

		// Act
		await fixture.Cache.SetAsync("cache-test", flag);

		// Assert
		var retrieved = await fixture.Cache.GetAsync("cache-test");
		retrieved.ShouldNotBeNull();
		retrieved.Key.ShouldBe("cache-test");
		retrieved.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagWithComplexData_ThenStoresCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = RedisFeatureFlagCacheFixture.CreateTestFlag("complex-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules =
		[
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = ["US", "CA", "UK"],
				Variation = "region-specific"
			}
		];
		flag.UserAccess = new FlagUserAccessControl(allowedUsers: ["admin1", "admin2"]);
		flag.Variations = new FlagVariations
		{
			Values = new Dictionary<string, object>()
				{
					{ "region-specific", new Dictionary<string, object> { { "currency", "USD" }, { "language", "en" } } }
				}
		};
		flag.Tags = new Dictionary<string, string>
		{
			{ "team", "platform" },
			{ "environment", "production" }
		};

		// Act
		await fixture.Cache.SetAsync("complex-flag", flag);

		// Assert
		var retrieved = await fixture.Cache.GetAsync("complex-flag");
		retrieved.ShouldNotBeNull();
		retrieved.TargetingRules.Count.ShouldBe(1);
		retrieved.TargetingRules[0].Attribute.ShouldBe("region");
		retrieved.TargetingRules[0].Values.ShouldContain("US");
		retrieved.UserAccess.IsUserExplicitlyManaged("admin1").ShouldBeTrue(); 
		retrieved.Tags.ShouldContainKeyAndValue("team", "platform");
	}

	[Fact]
	public async Task If_FlagWithExpiration_ThenExpiresCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = RedisFeatureFlagCacheFixture.CreateTestFlag("expiring-flag", FlagEvaluationMode.Enabled);
		var expiration = TimeSpan.FromSeconds(2);

		// Act
		await fixture.Cache.SetAsync("expiring-flag", flag, expiration);

		// Assert - Initially should exist
		var retrieved = await fixture.Cache.GetAsync("expiring-flag");
		retrieved.ShouldNotBeNull();

		// Wait for expiration
		await Task.Delay(TimeSpan.FromSeconds(3));

		// Should be expired now
		var expiredRetrieved = await fixture.Cache.GetAsync("expiring-flag");
		expiredRetrieved.ShouldBeNull();
	}

	[Fact]
	public async Task If_UpdateExistingFlag_ThenOverwritesData()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var originalFlag = RedisFeatureFlagCacheFixture.CreateTestFlag("update-flag", FlagEvaluationMode.Disabled);
		await fixture.Cache.SetAsync("update-flag", originalFlag);

		var updatedFlag = RedisFeatureFlagCacheFixture.CreateTestFlag("update-flag", FlagEvaluationMode.Enabled);
		updatedFlag.Name = "Updated Name";
		updatedFlag.Description = "Updated Description";

		// Act
		await fixture.Cache.SetAsync("update-flag", updatedFlag);

		// Assert
		var retrieved = await fixture.Cache.GetAsync("update-flag");
		retrieved.ShouldNotBeNull();
		retrieved.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled]).ShouldBeTrue();
		retrieved.Name.ShouldBe("Updated Name");
		retrieved.Description.ShouldBe("Updated Description");
	}
}

public class GetAsync_WhenFlagExists(RedisFeatureFlagCacheFixture fixture) : IClassFixture<RedisFeatureFlagCacheFixture>
{
	[Fact]
	public async Task ThenReturnsFlag()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = RedisFeatureFlagCacheFixture.CreateTestFlag("get-test", FlagEvaluationMode.Enabled);
		await fixture.Cache.SetAsync("get-test", flag);

		// Act
		var result = await fixture.Cache.GetAsync("get-test");

		// Assert
		result.ShouldNotBeNull();
		result.Key.ShouldBe("get-test");
		result.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled]).ShouldBeTrue();
		result.Name.ShouldBe(flag.Name);
	}

	[Fact]
	public async Task If_FlagWithTimeData_ThenDeserializesCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = RedisFeatureFlagCacheFixture.CreateTestFlag("time-flag", FlagEvaluationMode.TimeWindow);

		var schedule = FlagActivationSchedule.CreateSchedule(
			DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(7));
		var operationalWindow = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9), 
			TimeSpan.FromHours(17), 
			"America/New_York", 
			[DayOfWeek.Monday, DayOfWeek.Friday]);

		flag.ExpirationDate = DateTime.UtcNow.AddDays(30);
		flag.Schedule = schedule;
		flag.OperationalWindow = operationalWindow;

		await fixture.Cache.SetAsync("time-flag", flag);

		// Act
		var result = await fixture.Cache.GetAsync("time-flag");

		// Assert
		result.ShouldNotBeNull();
		result.ExpirationDate.ShouldNotBeNull();
		result.ExpirationDate.Value.ShouldBeInRange(flag.ExpirationDate.Value.AddSeconds(-1), flag.ExpirationDate.Value.AddSeconds(1));
		result.Schedule.ShouldBeEquivalentTo(schedule);
		result.OperationalWindow.ShouldBeEquivalentTo(operationalWindow);
	}

	[Fact]
	public async Task If_FlagWithUserRolloutPercentage_ThenDeserializesCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = RedisFeatureFlagCacheFixture.CreateTestFlag("percentage-flag", FlagEvaluationMode.UserRolloutPercentage);
		flag.UserAccess = new FlagUserAccessControl(rolloutPercentage: 75);

		await fixture.Cache.SetAsync("percentage-flag", flag);

		// Act
		var result = await fixture.Cache.GetAsync("percentage-flag");

		// Assert
		result.ShouldNotBeNull();
		result.UserAccess.RolloutPercentage.ShouldBe(75);
	}
}

public class GetAsync_WhenFlagDoesNotExist(RedisFeatureFlagCacheFixture fixture) : IClassFixture<RedisFeatureFlagCacheFixture>
{
	[Fact]
	public async Task ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllFlags();

		// Act
		var result = await fixture.Cache.GetAsync("non-existent-flag");

		// Assert
		result.ShouldBeNull();
	}
}

public class RemoveAsync_WhenFlagExists(RedisFeatureFlagCacheFixture fixture) : IClassFixture<RedisFeatureFlagCacheFixture>
{
	[Fact]
	public async Task ThenRemovesFlag()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = RedisFeatureFlagCacheFixture.CreateTestFlag("remove-test", FlagEvaluationMode.Enabled);
		await fixture.Cache.SetAsync("remove-test", flag);

		// Verify it exists first
		var beforeRemove = await fixture.Cache.GetAsync("remove-test");
		beforeRemove.ShouldNotBeNull();

		// Act
		await fixture.Cache.RemoveAsync("remove-test");

		// Assert
		var afterRemove = await fixture.Cache.GetAsync("remove-test");
		afterRemove.ShouldBeNull();
	}

	[Fact]
	public async Task If_RemoveNonExistentFlag_ThenDoesNotThrow()
	{
		// Arrange
		await fixture.ClearAllFlags();

		// Act & Assert - Should not throw
		await fixture.Cache.RemoveAsync("non-existent-flag");
	}
}

public class ClearAsync_WithMultipleFlags(RedisFeatureFlagCacheFixture fixture) : IClassFixture<RedisFeatureFlagCacheFixture>
{
	[Fact]
	public async Task ThenRemovesAllFlags()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag1 = RedisFeatureFlagCacheFixture.CreateTestFlag("flag-1", FlagEvaluationMode.Enabled);
		var flag2 = RedisFeatureFlagCacheFixture.CreateTestFlag("flag-2", FlagEvaluationMode.Disabled);
		var flag3 = RedisFeatureFlagCacheFixture.CreateTestFlag("flag-3", FlagEvaluationMode.UserRolloutPercentage);

		await fixture.Cache.SetAsync("flag-1", flag1);
		await fixture.Cache.SetAsync("flag-2", flag2);
		await fixture.Cache.SetAsync("flag-3", flag3);

		// Verify they exist
		(await fixture.Cache.GetAsync("flag-1")).ShouldNotBeNull();
		(await fixture.Cache.GetAsync("flag-2")).ShouldNotBeNull();
		(await fixture.Cache.GetAsync("flag-3")).ShouldNotBeNull();

		// Act
		await fixture.Cache.ClearAsync();

		// Assert
		(await fixture.Cache.GetAsync("flag-1")).ShouldBeNull();
		(await fixture.Cache.GetAsync("flag-2")).ShouldBeNull();
		(await fixture.Cache.GetAsync("flag-3")).ShouldBeNull();
	}

	[Fact]
	public async Task If_NoFlags_ThenDoesNotThrow()
	{
		// Arrange
		await fixture.ClearAllFlags();

		// Act & Assert - Should not throw
		await fixture.Cache.ClearAsync();
	}

	[Fact]
	public async Task If_OnlyFeatureFlagKeys_ThenDoesNotAffectOtherKeys()
	{
		// Arrange
		await fixture.ClearAllFlags();
		
		// Set a feature flag
		var flag = RedisFeatureFlagCacheFixture.CreateTestFlag("test-flag", FlagEvaluationMode.Enabled);
		await fixture.Cache.SetAsync("test-flag", flag);

		// Set a non-feature flag key directly in Redis
		var database = fixture.GetDatabase();
		await database.StringSetAsync("other:key", "some-value");

		// Act
		await fixture.Cache.ClearAsync();

		// Assert
		(await fixture.Cache.GetAsync("test-flag")).ShouldBeNull(); // Feature flag should be removed
		(await database.StringGetAsync("other:key")).HasValue.ShouldBeTrue(); // Other key should remain
	}
}

public class RedisFeatureFlagCache_KeyPrefix(RedisFeatureFlagCacheFixture fixture) : IClassFixture<RedisFeatureFlagCacheFixture>
{
	[Fact]
	public async Task If_SetAndGet_ThenUsesCorrectKeyPrefix()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = RedisFeatureFlagCacheFixture.CreateTestFlag("prefix-test", FlagEvaluationMode.Enabled);

		// Act
		await fixture.Cache.SetAsync("prefix-test", flag);

		// Assert
		var database = fixture.GetDatabase();
		var directValue = await database.StringGetAsync("ff:prefix-test");
		directValue.HasValue.ShouldBeTrue();

		// Verify we can retrieve through cache
		var retrieved = await fixture.Cache.GetAsync("prefix-test");
		retrieved.ShouldNotBeNull();
	}
}

public class RedisFeatureFlagCache_CancellationToken(RedisFeatureFlagCacheFixture fixture) : IClassFixture<RedisFeatureFlagCacheFixture>
{
	[Fact]
	public async Task If_CancellationRequested_ThenOperationsCancelled()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = RedisFeatureFlagCacheFixture.CreateTestFlag("cancellation-test", FlagEvaluationMode.Enabled);
		await fixture.Cache.SetAsync("cancellation-test", flag);

		using var cts = new CancellationTokenSource();
		cts.Cancel(); // Cancel immediately

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => fixture.Cache.GetAsync("cancellation-test", cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => fixture.Cache.SetAsync("new-flag", flag, null, cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => fixture.Cache.RemoveAsync("cancellation-test", cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => fixture.Cache.ClearAsync(cts.Token));
	}
}

public class RedisFeatureFlagCache_ErrorHandling(RedisFeatureFlagCacheFixture fixture) : IClassFixture<RedisFeatureFlagCacheFixture>
{
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
		await fixture.ClearAllFlags();
		var database = fixture.GetDatabase();
		
		// Set corrupted JSON data directly
		await database.StringSetAsync("ff:corrupted-flag", "invalid-json-data");

		// Act
		var result = await fixture.Cache.GetAsync("corrupted-flag");

		// Assert
		result.ShouldBeNull(); // Should return null instead of throwing
	}
}

public class RedisFeatureFlagCacheFixture : IAsyncLifetime
{
	private readonly RedisContainer _container;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	private ConnectionMultiplexer? _connectionMultiplexer = null!;
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

	// Helper method to clear all feature flags between tests
	public async Task ClearAllFlags()
	{
		await Cache.ClearAsync();
	}

	public static FeatureFlag CreateTestFlag(string key, FlagEvaluationMode evaluationMode)
	{
		var flag = new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for integration tests",
		};
		flag.EvaluationModeSet.AddMode(evaluationMode);
		return flag;
	}
}