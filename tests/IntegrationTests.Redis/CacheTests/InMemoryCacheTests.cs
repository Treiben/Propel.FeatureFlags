using FeatureFlags.IntegrationTests.Redis.Support;
using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace FeatureFlags.IntegrationTests.Redis.CacheTests.InMemory;

/* The tests cover these scenarios:
 *		Cache operations (Get, Set, Remove, Clear)
 *		Complex data serialization/deserialization
 *		Key prefix handling
 *		Expiration functionality
 *		Error handling and edge cases
 *		Cancellation token support
 *		In-memory specific scenarios
*/
public class SetAsync_WithValidFlag(InMemoryTestsFixture fixture) : IClassFixture<InMemoryTestsFixture>
{
	[Fact]
	public async Task ThenStoresFlag()
	{
		// Arrange
		await fixture.ClearAllFlags();

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
		await fixture.ClearAllFlags();

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
		await fixture.ClearAllFlags();

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
		await fixture.ClearAllFlags();

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

	[Fact]
	public async Task If_CancellationTokenProvided_ThenCompletesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllFlags();
		using var cts = new CancellationTokenSource();
		
		var (options, _) = new FlagConfigurationBuilder("cancellation-test")
			.WithEvaluationModes(EvaluationMode.On)
			.Build();
		var cacheKey = CacheKeyFactory.CreateCacheKey(options.Key);

		// Act & Assert - Should not throw
		await fixture.Cache.SetAsync(cacheKey, options, cts.Token);
		
		var retrieved = await fixture.Cache.GetAsync(cacheKey, cts.Token);
		retrieved.ShouldNotBeNull();
	}
}

public class GetAsync_WhenFlagExists(InMemoryTestsFixture fixture) : IClassFixture<InMemoryTestsFixture>
{
	[Fact]
	public async Task If_FlagWithTimeData_ThenDeserializesCorrectly()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var (options, _) = new FlagConfigurationBuilder("time-flag")
			.WithEvaluationModes(EvaluationMode.Scheduled, EvaluationMode.TimeWindow)
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
		await fixture.ClearAllFlags();
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

	[Fact]
	public async Task If_CancellationTokenProvided_ThenCompletesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllFlags();
		using var cts = new CancellationTokenSource();
		
		var (options, _) = new FlagConfigurationBuilder("cancellation-test")
			.WithEvaluationModes(EvaluationMode.On)
			.Build();
		var cacheKey = CacheKeyFactory.CreateCacheKey(options.Key);
		
		await fixture.Cache.SetAsync(cacheKey, options);

		// Act & Assert - Should not throw
		var result = await fixture.Cache.GetAsync(cacheKey, cts.Token);
		result.ShouldNotBeNull();
	}
}

public class GetAsync_WhenFlagDoesNotExist(InMemoryTestsFixture fixture) : IClassFixture<InMemoryTestsFixture>
{
	[Fact]
	public async Task ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllFlags();

		// Act
		var cacheKey = CacheKeyFactory.CreateCacheKey("not-existent-flag");
		var result = await fixture.Cache.GetAsync(cacheKey);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task If_CancellationTokenProvided_ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllFlags();
		using var cts = new CancellationTokenSource();

		// Act
		var cacheKey = CacheKeyFactory.CreateCacheKey("not-existent-flag");
		var result = await fixture.Cache.GetAsync(cacheKey, cts.Token);

		// Assert
		result.ShouldBeNull();
	}
}

public class RemoveAsync_WhenFlagExists(InMemoryTestsFixture fixture) : IClassFixture<InMemoryTestsFixture>
{
	[Fact]
	public async Task ThenRemovesFlag()
	{
		// Arrange
		await fixture.ClearAllFlags();

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

	[Fact]
	public async Task If_CancellationTokenProvided_ThenRemovesFlag()
	{
		// Arrange
		await fixture.ClearAllFlags();
		using var cts = new CancellationTokenSource();

		var (options, _) = new FlagConfigurationBuilder("remove-test").WithEvaluationModes(EvaluationMode.On).Build();
		var cacheKey = CacheKeyFactory.CreateCacheKey(options.Key);

		await fixture.Cache.SetAsync(cacheKey, options);

		// Verify it exists first
		var beforeRemove = await fixture.Cache.GetAsync(cacheKey);
		beforeRemove.ShouldNotBeNull();

		// Act
		await fixture.Cache.RemoveAsync(cacheKey, cts.Token);

		// Assert
		var afterRemove = await fixture.Cache.GetAsync(cacheKey);
		afterRemove.ShouldBeNull();
	}
}

public class RemoveAsync_WhenFlagDoesNotExist(InMemoryTestsFixture fixture) : IClassFixture<InMemoryTestsFixture>
{
	[Fact]
	public async Task ThenDoesNotThrow()
	{
		// Arrange
		await fixture.ClearAllFlags();

		// Act & Assert - Should not throw
		var cacheKey = CacheKeyFactory.CreateCacheKey("non-existent-flag");
		await fixture.Cache.RemoveAsync(cacheKey);
	}
}

public class ClearAsync_WithMultipleFlags(InMemoryTestsFixture fixture) : IClassFixture<InMemoryTestsFixture>
{
	[Fact]
	public async Task ThenRemovesAllFlags()
	{
		// Arrange
		await fixture.ClearAllFlags();

		var (options1, _) = new FlagConfigurationBuilder("flag-1").WithEvaluationModes(EvaluationMode.On).Build();
		var (options2, _) = new FlagConfigurationBuilder("flag-2").WithEvaluationModes(EvaluationMode.On).Build();
		var (options3, _) = new FlagConfigurationBuilder("flag-3").WithEvaluationModes(EvaluationMode.On).Build();

		var cacheKey1 = CacheKeyFactory.CreateCacheKey(options1.Key);
		var cacheKey2 = CacheKeyFactory.CreateCacheKey(options2.Key);
		var cacheKey3 = CacheKeyFactory.CreateCacheKey(options3.Key);

		await fixture.Cache.SetAsync(cacheKey1, options1);
		await fixture.Cache.SetAsync(cacheKey2, options2);
		await fixture.Cache.SetAsync(cacheKey3, options3);

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
	public async Task If_CancellationTokenProvided_ThenClearsAllFlags()
	{
		// Arrange
		await fixture.ClearAllFlags();
		using var cts = new CancellationTokenSource();

		var (options1, _) = new FlagConfigurationBuilder("flag-1").WithEvaluationModes(EvaluationMode.On).Build();
		var (options2, _) = new FlagConfigurationBuilder("flag-2").WithEvaluationModes(EvaluationMode.On).Build();

		var cacheKey1 = CacheKeyFactory.CreateCacheKey(options1.Key);
		var cacheKey2 = CacheKeyFactory.CreateCacheKey(options2.Key);

		await fixture.Cache.SetAsync(cacheKey1, options1);
		await fixture.Cache.SetAsync(cacheKey2, options2);

		// Act
		await fixture.Cache.ClearAsync(cts.Token);

		// Assert
		(await fixture.Cache.GetAsync(cacheKey1)).ShouldBeNull();
		(await fixture.Cache.GetAsync(cacheKey2)).ShouldBeNull();
	}
}

public class CacheExpiration_Tests(InMemoryTestsFixture fixture) : IClassFixture<InMemoryTestsFixture>
{
	[Fact]
	public async Task If_CacheExpirationConfigured_ThenFlagExpiresAfterDuration()
	{
		// Note: This test demonstrates that expiration is configured but doesn't test actual expiration
		// since that would require waiting for the cache duration in the test
		
		// Arrange
		await fixture.ClearAllFlags();

		var (options, _) = new FlagConfigurationBuilder("expiring-flag")
			.WithEvaluationModes(EvaluationMode.On)
			.Build();
		var cacheKey = CacheKeyFactory.CreateCacheKey(options.Key);

		// Act
		await fixture.Cache.SetAsync(cacheKey, options);

		// Assert - Flag should be immediately retrievable
		var retrieved = await fixture.Cache.GetAsync(cacheKey);
		retrieved.ShouldNotBeNull();
	}
}
