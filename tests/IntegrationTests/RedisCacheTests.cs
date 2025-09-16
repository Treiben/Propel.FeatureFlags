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

		var (flag, _) = TestHelpers.SetupTestCases("cache-test", EvaluationMode.Enabled);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key.Key);

		// Act
		await fixture.Cache.SetAsync(cacheKey, new EvaluationCriteria
		{
			FlagKey = flag.Key.Key,
			ActiveEvaluationModes = flag.ActiveEvaluationModes,
		});

		// Assert
		var retrieved = await fixture.Cache.GetAsync(cacheKey);
		retrieved.ShouldNotBeNull();
		retrieved.FlagKey.ShouldBe("cache-test");
		retrieved.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagWithComplexData_ThenStoresCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var (flag, _) = TestHelpers.SetupTestCases("complex-flag", EvaluationMode.UserTargeted);
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
		await fixture.Cache.SetAsync(cacheKey, new EvaluationCriteria
		{
			FlagKey = flag.Key.Key,
			ActiveEvaluationModes = flag.ActiveEvaluationModes,
			TargetingRules = flag.TargetingRules,
			UserAccessControl = flag.UserAccessControl,
			Variations = flag.Variations
		});

		// Assert
		var retrieved = await fixture.Cache.GetAsync(cacheKey);

		retrieved.ShouldNotBeNull();

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
		var (flag, _) = TestHelpers.SetupTestCases("expiring-flag", EvaluationMode.Enabled);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key.Key);

		// Act
		await fixture.Cache.SetAsync(cacheKey, new EvaluationCriteria
		{
			FlagKey = flag.Key.Key,
			ActiveEvaluationModes = flag.ActiveEvaluationModes,
		});
		var retrieved = await fixture.Cache.GetAsync(cacheKey);
		retrieved.ShouldNotBeNull();
	}

	[Fact]
	public async Task If_UpdateExistingFlag_ThenOverwritesData()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var (originalFlag, _) = TestHelpers.SetupTestCases("update-flag", EvaluationMode.Disabled);
		var currentApplicationName = ApplicationInfo.Name;
		var currentApplicationVersion = ApplicationInfo.Version;

		var cacheKey = new CacheKey("update-flag", [currentApplicationName, currentApplicationVersion]);
		await fixture.Cache.SetAsync(cacheKey, new EvaluationCriteria
			{
				FlagKey = originalFlag.Key.Key,
				ActiveEvaluationModes = originalFlag.ActiveEvaluationModes,
			});

		var (updatedFlag, _) = TestHelpers.SetupTestCases("update-flag", EvaluationMode.Enabled);
		updatedFlag.Name = "Updated Name";
		updatedFlag.Description = "Updated Description";

		// Act
		await fixture.Cache.SetAsync(cacheKey, new EvaluationCriteria { FlagKey = updatedFlag.Key.Key, ActiveEvaluationModes = updatedFlag.ActiveEvaluationModes });

		// Assert
		var retrieved = await fixture.Cache.GetAsync(cacheKey);
		retrieved.ShouldNotBeNull();
		retrieved.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();
	}
}

public class GetAsync_WhenFlagExists(RedisTestsFixture fixture) : IClassFixture<RedisTestsFixture>
{
	[Fact]
	public async Task ThenReturnsFlag()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var (flag, _) = TestHelpers.SetupTestCases("get-test", EvaluationMode.Enabled);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key.Key);

		await fixture.Cache.SetAsync(cacheKey, new EvaluationCriteria { FlagKey = flag.Key.Key, ActiveEvaluationModes = flag.ActiveEvaluationModes });

		// Act
		var result = await fixture.Cache.GetAsync(cacheKey);

		// Assert
		result.ShouldNotBeNull();
		result.FlagKey.ShouldBe("get-test");
		result.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagWithTimeData_ThenDeserializesCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var (flag, _) = TestHelpers.SetupTestCases("time-flag", EvaluationMode.TimeWindow);

		var schedule = ActivationSchedule.CreateSchedule(
			DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(7));
		var operationalWindow = new OperationalWindow(
			TimeSpan.FromHours(9), 
			TimeSpan.FromHours(17), 
			"America/New_York", 
			[DayOfWeek.Monday, DayOfWeek.Friday]);

		flag.Schedule = schedule;
		flag.OperationalWindow = operationalWindow;

		var cacheKey = TestHelpers.CreateGlobalCacheKey(flag.Key.Key);

		await fixture.Cache.SetAsync(cacheKey, new EvaluationCriteria { 
			FlagKey = flag.Key.Key,
			ActiveEvaluationModes = flag.ActiveEvaluationModes,
			Schedule = flag.Schedule,
			OperationalWindow = flag.OperationalWindow
		});

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
		var (flag, _) = TestHelpers.SetupTestCases("percentage-flag", EvaluationMode.UserRolloutPercentage);
		flag.UserAccessControl = new AccessControl(rolloutPercentage: 75);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key.Key);

		await fixture.Cache.SetAsync(cacheKey, new EvaluationCriteria
		{
			FlagKey = flag.Key.Key,
			ActiveEvaluationModes = flag.ActiveEvaluationModes,
			UserAccessControl = flag.UserAccessControl
		});

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
		var (flag, _) = TestHelpers.SetupTestCases("remove-test", EvaluationMode.Enabled);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key.Key);

		await fixture.Cache.SetAsync(cacheKey, new EvaluationCriteria { FlagKey = flag.Key.Key, ActiveEvaluationModes = flag.ActiveEvaluationModes });

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
		var (flag1, _) = TestHelpers.SetupTestCases("flag-1", EvaluationMode.Enabled);
		var (flag2, _) = TestHelpers.SetupTestCases("flag-2", EvaluationMode.Disabled);
		var (flag3, _) = TestHelpers.SetupTestCases("flag-3", EvaluationMode.UserRolloutPercentage);


		var cacheKey1 = TestHelpers.CreateCacheKey(flag1.Key.Key);
		var cacheKey2 = TestHelpers.CreateCacheKey(flag2.Key.Key);
		var cacheKey3 = TestHelpers.CreateCacheKey(flag3.Key.Key);

		await fixture.Cache.SetAsync(cacheKey1, new EvaluationCriteria { FlagKey = flag1.Key.Key, ActiveEvaluationModes = flag1.ActiveEvaluationModes });
		await fixture.Cache.SetAsync(cacheKey2, new EvaluationCriteria { FlagKey = flag2.Key.Key, ActiveEvaluationModes = flag2.ActiveEvaluationModes });
		await fixture.Cache.SetAsync(cacheKey3, new EvaluationCriteria { FlagKey = flag3.Key.Key, ActiveEvaluationModes = flag3.ActiveEvaluationModes });

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
		var (flag, _) = TestHelpers.SetupTestCases("test-flag", EvaluationMode.Enabled);
		var cacheKey = TestHelpers.CreateCacheKey(flag.Key.Key);
		await fixture.Cache.SetAsync(cacheKey, new EvaluationCriteria { FlagKey = flag.Key.Key, ActiveEvaluationModes = flag.ActiveEvaluationModes });

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