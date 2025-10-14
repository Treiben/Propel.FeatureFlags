using FeatureFlags.IntegrationTests.Redis.Support;
using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace FeatureFlags.IntegrationTests.Redis.CacheTests;

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
		var (options, _) = new FlagConfigurationBuilder("cache-test")
							.WithEvaluationModes(EvaluationMode.On)
							.Build();
		var cacheKey = CacheKeyFactory.CreateCacheKey(options.Key);

		// Act
		await fixture.Cache.SetAsync(cacheKey, options);

		// Assert
		var retrieved = await fixture.Cache.GetAsync(cacheKey);
		retrieved.ShouldNotBeNull();
		retrieved.Key.ShouldBe("cache-test");
		retrieved.ModeSet.Contains([EvaluationMode.On]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagWithComplexData_ThenStoresCorrectly()
	{
		// Arrange
		var (options, _) = new FlagConfigurationBuilder("complex-flag")
			.WithEvaluationModes(EvaluationMode.UserTargeted)
			.WithTargetingRules([
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
				])
			.WithUserAccessControl(new AccessControl(allowed: ["admin1", "admin2"]))
			.WithVariations(new Variations
			{
				Values = new Dictionary<string, object> { { "region-specific", new Dictionary<string, object> { { "currency", "USD" }, { "language", "en" } } } },
				DefaultVariation = "default"
			})
			.Build();

		var cacheKey = CacheKeyFactory.CreateCacheKey("complex-flag");

		// Act
		await fixture.Cache.SetAsync(cacheKey, options);

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
		var (options, _) = new FlagConfigurationBuilder("expiring-flag")
			.WithEvaluationModes(EvaluationMode.On)
			.Build();

		var cacheKey = CacheKeyFactory.CreateCacheKey(options.Key);

		// Act
		await fixture.Cache.SetAsync(cacheKey, options);
		var retrieved = await fixture.Cache.GetAsync(cacheKey);
		retrieved.ShouldNotBeNull();
	}

	[Fact]
	public async Task If_UpdateExistingFlag_ThenOverwritesData()
	{
		// Arrange
		var (oldOptions, _) = new FlagConfigurationBuilder("original-flag")
			.WithEvaluationModes(EvaluationMode.Off)
			.Build();
		var currentApplicationName = ApplicationInfo.Name;
		var currentApplicationVersion = ApplicationInfo.Version;

		var cacheKey = new CacheKey("original-flag", [currentApplicationName, currentApplicationVersion]);
		await fixture.Cache.SetAsync(cacheKey, oldOptions);

		var (updatedFlag, _) = new FlagConfigurationBuilder("original-flag")
			.WithEvaluationModes(EvaluationMode.On)
			.Build();

		// Act
		await fixture.Cache.SetAsync(cacheKey, updatedFlag);

		// Assert
		var retrieved = await fixture.Cache.GetAsync(cacheKey);
		retrieved.ShouldNotBeNull();
		retrieved.ModeSet.Contains([EvaluationMode.On]).ShouldBeTrue();
	}
}

public class GetAsync_WhenFlagExists(RedisTestsFixture fixture) : IClassFixture<RedisTestsFixture>
{
	[Fact]
	public async Task If_FlagWithTimeData_ThenDeserializesCorrectly()
	{
		// Arrange
		var (options, _) = new FlagConfigurationBuilder("time-flag")
			.WithEvaluationModes(EvaluationMode.Scheduled,EvaluationMode.TimeWindow)
			.WithSchedule(UtcSchedule.CreateSchedule(DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddDays(7)))
			.WithOperationalWindow(new UtcTimeWindow(
					TimeSpan.FromHours(9),
					TimeSpan.FromHours(17),
					"America/New_York",
					[DayOfWeek.Monday, DayOfWeek.Friday]))
			.Build();

		var cacheKey = CacheKeyFactory.CreateGlobalCacheKey(options.Key);

		await fixture.Cache.SetAsync(cacheKey, options);

		// Act
		var result = await fixture.Cache.GetAsync(cacheKey);

		// Assert
		result.ShouldNotBeNull();
		result.Schedule.ShouldBeEquivalentTo(options.Schedule);
		result.OperationalWindow.ShouldBeEquivalentTo(options.OperationalWindow);
	}

	[Fact]
	public async Task If_FlagWithUserRolloutPercentage_ThenDeserializesCorrectly()
	{
		// Arrange
		var (options, _) = new FlagConfigurationBuilder("percentage-flag")
							.WithEvaluationModes(EvaluationMode.UserRolloutPercentage)
							.WithUserAccessControl(new AccessControl(rolloutPercentage: 75))
							.Build();

		var cacheKey = CacheKeyFactory.CreateCacheKey(options.Key);

		await fixture.Cache.SetAsync(cacheKey, options);

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
		// Act
		var cacheKey = CacheKeyFactory.CreateCacheKey("not-existent-flag");
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
		var (options, _) = new FlagConfigurationBuilder("remove-test").WithEvaluationModes(EvaluationMode.On).Build();
		var cacheKey = CacheKeyFactory.CreateCacheKey(options.Key);

		await fixture.Cache.SetAsync(cacheKey, options);

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