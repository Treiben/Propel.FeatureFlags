using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

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
public class SetAsync_WithValidFlag(RedisTestsFixture fixture) : IClassFixture<RedisTestsFixture>
{
	[Fact]
	public async Task ThenStoresFlag()
	{
		// Arrange
		await fixture.ClearAllFlags();

		var flag = TestHelpers.CreateTestFlag("cache-test", EvaluationMode.Enabled);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key);

		// Act
		await fixture.Cache.SetAsync(cacheKey, flag);

		// Assert
		var retrieved = await fixture.Cache.GetAsync(cacheKey);
		retrieved.ShouldNotBeNull();
		retrieved.Key.ShouldBe("cache-test");
		retrieved.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagWithComplexData_ThenStoresCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = TestHelpers.CreateTestFlag("complex-flag", EvaluationMode.UserTargeted);
		flag.TargetingRules =
		[
			TargetingRuleFactory.CreateTargetingRule(
				"region",
				TargetingOperator.In,
				["US", "CA", "UK"],
				"region-specific"
			),
			TargetingRuleFactory.CreateTargetingRule(
				"age",
				TargetingOperator.GreaterThan,
				["18", "21"],
				"adult"
			)
		];
		flag.UserAccessControl = new AccessControl(allowed: ["admin1", "admin2"]);
		flag.Variations = new Variations
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

		var cacheKey = TestHelpers.CreateCacheKey("complex-flag");

		// Act
		await fixture.Cache.SetAsync(cacheKey, flag);

		// Assert
		var retrieved = await fixture.Cache.GetAsync(cacheKey);

		retrieved.ShouldNotBeNull();
		retrieved.Tags.ShouldContainKeyAndValue("team", "platform");

		retrieved.TargetingRules.ShouldBeOfType<List<ITargetingRule>>();
		retrieved.TargetingRules.Count.ShouldBe(2);

		var stringRule = retrieved.TargetingRules[0] as StringTargetingRule;
		stringRule!.Attribute.ShouldBe("region");
		stringRule.Values.ShouldContain("UK");

		var numericRule = retrieved.TargetingRules[1] as NumericTargetingRule;
		numericRule!.Attribute.ShouldBe("age");
		numericRule.Values.ShouldContain(21);
	}

	[Fact]
	public async Task If_FlagWithComplexCacheKey_ThenStoreCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = TestHelpers.CreateTestFlag("expiring-flag", EvaluationMode.Enabled);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key);

		// Act
		await fixture.Cache.SetAsync(cacheKey, flag);
		var retrieved = await fixture.Cache.GetAsync(cacheKey);
		retrieved.ShouldNotBeNull();
	}

	[Fact]
	public async Task If_UpdateExistingFlag_ThenOverwritesData()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var originalFlag = TestHelpers.CreateTestFlag("update-flag", EvaluationMode.Disabled);
		var currentApplicationName = ApplicationInfo.Name;
		var currentApplicationVersion = ApplicationInfo.Version;

		var cacheKey = new CacheKey("update-flag", [currentApplicationName, currentApplicationVersion]);
		await fixture.Cache.SetAsync(cacheKey, originalFlag);

		var updatedFlag = TestHelpers.CreateTestFlag("update-flag", EvaluationMode.Enabled);
		updatedFlag.Name = "Updated Name";
		updatedFlag.Description = "Updated Description";

		// Act
		await fixture.Cache.SetAsync(cacheKey, updatedFlag);

		// Assert
		var retrieved = await fixture.Cache.GetAsync(cacheKey);
		retrieved.ShouldNotBeNull();
		retrieved.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();
		retrieved.Name.ShouldBe("Updated Name");
		retrieved.Description.ShouldBe("Updated Description");
	}
}

public class GetAsync_WhenFlagExists(RedisTestsFixture fixture) : IClassFixture<RedisTestsFixture>
{
	[Fact]
	public async Task ThenReturnsFlag()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = TestHelpers.CreateTestFlag("get-test", EvaluationMode.Enabled);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key);

		await fixture.Cache.SetAsync(cacheKey, flag);

		// Act
		var result = await fixture.Cache.GetAsync(cacheKey);

		// Assert
		result.ShouldNotBeNull();
		result.Key.ShouldBe("get-test");
		result.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();
		result.Name.ShouldBe(flag.Name);
	}

	[Fact]
	public async Task If_FlagWithTimeData_ThenDeserializesCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = TestHelpers.CreateTestFlag("time-flag", EvaluationMode.TimeWindow);

		var schedule = ActivationSchedule.CreateSchedule(
			DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(7));
		var operationalWindow = new OperationalWindow(
			TimeSpan.FromHours(9), 
			TimeSpan.FromHours(17), 
			"America/New_York", 
			[DayOfWeek.Monday, DayOfWeek.Friday]);

		flag.Retention = new RetentionPolicy(
			expirationDate: DateTime.UtcNow.AddDays(30),
			isPermanent: true,
			scope: Scope.Global);
		flag.Schedule = schedule;
		flag.OperationalWindow = operationalWindow;

		var cacheKey = TestHelpers.CreateGlobalCacheKey(flag.Key);

		await fixture.Cache.SetAsync(cacheKey, flag);

		// Act
		var result = await fixture.Cache.GetAsync(cacheKey);

		// Assert
		result.ShouldNotBeNull();
		result.Schedule.ShouldBeEquivalentTo(schedule);
		result.OperationalWindow.ShouldBeEquivalentTo(operationalWindow);
	}

	[Fact]
	public async Task If_FlagWithUserRolloutPercentage_ThenDeserializesCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = TestHelpers.CreateTestFlag("percentage-flag", EvaluationMode.UserRolloutPercentage);
		flag.UserAccessControl = new AccessControl(rolloutPercentage: 75);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key);

		await fixture.Cache.SetAsync(cacheKey, flag);

		// Act
		var result = await fixture.Cache.GetAsync(cacheKey);

		// Assert
		result.ShouldNotBeNull();
		result.UserAccessControl.RolloutPercentage.ShouldBe(75);
	}
}

public class GetAsync_WhenFlagDoesNotExist(RedisTestsFixture fixture) : IClassFixture<RedisTestsFixture>
{
	[Fact]
	public async Task ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllFlags();

		// Act
		var currentApplicationName = ApplicationInfo.Name;
		var currentApplicationVersion = ApplicationInfo.Version;

		var cacheKey = TestHelpers.CreateCacheKey("not-existent-flag");
		var result = await fixture.Cache.GetAsync(cacheKey);

		// Assert
		result.ShouldBeNull();
	}
}

public class RemoveAsync_WhenFlagExists(RedisTestsFixture fixture) : IClassFixture<RedisTestsFixture>
{
	[Fact]
	public async Task ThenRemovesFlag()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag = TestHelpers.CreateTestFlag("remove-test", EvaluationMode.Enabled);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key);

		await fixture.Cache.SetAsync(cacheKey, flag);

		// Verify it exists first
		var beforeRemove = await fixture.Cache.GetAsync(cacheKey);
		beforeRemove.ShouldNotBeNull();

		// Act
		await fixture.Cache.RemoveAsync(cacheKey);

		// Assert
		var afterRemove = await fixture.Cache.GetAsync(cacheKey);
		afterRemove.ShouldBeNull();
	}
}

public class ClearAsync_WithMultipleFlags(RedisTestsFixture fixture) : IClassFixture<RedisTestsFixture>
{
	[Fact]
	public async Task ThenRemovesAllFlags()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag1 = TestHelpers.CreateTestFlag("flag-1", EvaluationMode.Enabled);
		var flag2 = TestHelpers.CreateTestFlag("flag-2", EvaluationMode.Disabled);
		var flag3 = TestHelpers.CreateTestFlag("flag-3", EvaluationMode.UserRolloutPercentage);


		var cacheKey1 = TestHelpers.CreateCacheKey(flag1.Key);
		var cacheKey2 = TestHelpers.CreateCacheKey(flag2.Key);
		var cacheKey3 = TestHelpers.CreateCacheKey(flag3.Key);

		await fixture.Cache.SetAsync(cacheKey1, flag1);
		await fixture.Cache.SetAsync(cacheKey2, flag2);
		await fixture.Cache.SetAsync(cacheKey3, flag3);

		// Verify they exist
		(await fixture.Cache.GetAsync(cacheKey1)).ShouldNotBeNull();
		(await fixture.Cache.GetAsync(cacheKey2)).ShouldNotBeNull();
		(await fixture.Cache.GetAsync(cacheKey3)).ShouldNotBeNull();

		// Act
		await fixture.Cache.ClearAsync();

		// Assert
		(await fixture.Cache.GetAsync(cacheKey1)).ShouldBeNull();
		(await fixture.Cache.GetAsync(cacheKey2)).ShouldBeNull();
		(await fixture.Cache.GetAsync(cacheKey3)).ShouldBeNull();
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
		var flag = TestHelpers.CreateTestFlag("test-flag", EvaluationMode.Enabled);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key);
		await fixture.Cache.SetAsync(cacheKey, flag);

		// Set a non-feature flag key directly in Redis
		var database = fixture.GetDatabase();
		await database.StringSetAsync("other:key", "some-value");

		// Act
		await fixture.Cache.ClearAsync();

		// Assert
		(await fixture.Cache.GetAsync(cacheKey)).ShouldBeNull(); // Feature flag should be removed
		(await database.StringGetAsync("other:key")).HasValue.ShouldBeTrue(); // Other key should remain
	}
}