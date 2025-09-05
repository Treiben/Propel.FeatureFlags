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
		var flag = _fixture.CreateTestFlag("cache-test", FlagEvaluationMode.Enabled);

		// Act
		await _fixture.Cache.SetAsync("cache-test", flag);

		// Assert
		var retrieved = await _fixture.Cache.GetAsync("cache-test");
		retrieved.ShouldNotBeNull();
		retrieved.Key.ShouldBe("cache-test");
		retrieved.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagWithComplexData_ThenStoresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("complex-flag", FlagEvaluationMode.UserTargeted);
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
		flag.UserAccess = FlagUserAccessControl.CreateAccessControl(allowedUsers: ["admin1", "admin2"]);
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
		await _fixture.Cache.SetAsync("complex-flag", flag);

		// Assert
		var retrieved = await _fixture.Cache.GetAsync("complex-flag");
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
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("expiring-flag", FlagEvaluationMode.Enabled);
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
		var originalFlag = _fixture.CreateTestFlag("update-flag", FlagEvaluationMode.Disabled);
		await _fixture.Cache.SetAsync("update-flag", originalFlag);

		var updatedFlag = _fixture.CreateTestFlag("update-flag", FlagEvaluationMode.Enabled);
		updatedFlag.Name = "Updated Name";
		updatedFlag.Description = "Updated Description";

		// Act
		await _fixture.Cache.SetAsync("update-flag", updatedFlag);

		// Assert
		var retrieved = await _fixture.Cache.GetAsync("update-flag");
		retrieved.ShouldNotBeNull();
		retrieved.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled]).ShouldBeTrue();
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
		var flag = _fixture.CreateTestFlag("get-test", FlagEvaluationMode.Enabled);
		await _fixture.Cache.SetAsync("get-test", flag);

		// Act
		var result = await _fixture.Cache.GetAsync("get-test");

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
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("time-flag", FlagEvaluationMode.TimeWindow);

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

		await _fixture.Cache.SetAsync("time-flag", flag);

		// Act
		var result = await _fixture.Cache.GetAsync("time-flag");

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
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("percentage-flag", FlagEvaluationMode.UserRolloutPercentage);
		flag.UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 75);

		await _fixture.Cache.SetAsync("percentage-flag", flag);

		// Act
		var result = await _fixture.Cache.GetAsync("percentage-flag");

		// Assert
		result.ShouldNotBeNull();
		result.UserAccess.RolloutPercentage.ShouldBe(75);
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
		var flag = _fixture.CreateTestFlag("remove-test", FlagEvaluationMode.Enabled);
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
		var flag1 = _fixture.CreateTestFlag("flag-1", FlagEvaluationMode.Enabled);
		var flag2 = _fixture.CreateTestFlag("flag-2", FlagEvaluationMode.Disabled);
		var flag3 = _fixture.CreateTestFlag("flag-3", FlagEvaluationMode.UserRolloutPercentage);

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
		var flag = _fixture.CreateTestFlag("test-flag", FlagEvaluationMode.Enabled);
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
		var flag = _fixture.CreateTestFlag("prefix-test", FlagEvaluationMode.Enabled);

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
		var flag = _fixture.CreateTestFlag("cancellation-test", FlagEvaluationMode.Enabled);
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

	public FeatureFlag CreateTestFlag(string key, FlagEvaluationMode evaluationMode)
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

	// Helper method to clear all feature flags between tests
	public async Task ClearAllFlags()
	{
		await Cache.ClearAsync();
	}
}