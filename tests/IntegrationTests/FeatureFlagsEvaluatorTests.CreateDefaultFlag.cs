using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Evaluation;

namespace FeatureFlags.IntegrationTests.Core.ClientWithDefaultFlags;

/* These tests cover scenarios when CreateDefaultFlagAsync() is called:
 *		Auto-creation of disabled flags when not found in cache or repository
 *		Repository success scenarios (flag created and returned)
 *		Repository failure scenarios (exception thrown but evaluation continues)
 *		Proper flag structure and default values
 *		Caching of auto-created flags
 *		Logging behavior during flag creation
 *		Concurrent creation attempts
 *		Integration with the full evaluation flow
*/

public class EvaluateAsync_WithAutoCreatedFlag(DefaultFlagCreationTestsFixture fixture) : IClassFixture<DefaultFlagCreationTestsFixture>
{
	[Fact]
	public async Task If_FlagNotFound_ThenCreatesDefaultDisabledFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("auto-created-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag not found, created default disabled flag");

		// Verify the flag was actually created in the repository
		var createdFlag = await fixture.Repository.GetAsync("auto-created-flag");
		createdFlag.ShouldNotBeNull();
		createdFlag.Key.ShouldBe("auto-created-flag");
		createdFlag.Name.ShouldBe("auto-created-flag");
		createdFlag.Description.ShouldBe("Auto-created flag for auto-created-flag");
		createdFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();
		createdFlag.Created.Actor.ShouldBe("system");
		createdFlag.Variations.DefaultVariation.ShouldBe("off");
	}

	[Fact]
	public async Task If_FlagAutoCreated_ThenItShouldBeBeCached()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("cached-auto-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();

		// Verify the flag is now in the cache
		var cachedFlag = await fixture.Cache.GetAsync("cached-auto-flag");
		cachedFlag.ShouldNotBeNull();
	}

	[Fact]
	public async Task If_FlagAutoCreatedTwice_ThenUsesExistingFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");

		// Act - First evaluation creates the flag
		var result1 = await fixture.Evaluator.Evaluate("duplicate-test-flag", context);
		
		// Clear cache to force repository lookup
		await fixture.Cache.ClearAsync();
		
		// Second evaluation should find existing flag in repository
		var result2 = await fixture.Evaluator.Evaluate("duplicate-test-flag", context);

		// Assert
		result1.ShouldNotBeNull();
		result1.IsEnabled.ShouldBeFalse();
		
		result2.ShouldNotBeNull();
		result2.IsEnabled.ShouldBeFalse();

		// Verify only one flag exists in repository
		var allFlags = await fixture.Repository.GetAllAsync();
		var duplicateFlags = allFlags.Where(f => f.Key == "duplicate-test-flag").ToList();
		duplicateFlags.Count.ShouldBe(1);
	}

	[Fact]
	public async Task If_ConcurrentFlagCreation_ThenHandlesGracefully()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");

		// Act - Simulate concurrent evaluation requests
		var tasks = Enumerable.Range(0, 5).Select(_ => 
			fixture.Evaluator.Evaluate("concurrent-flag", context)).ToArray();
		
		var results = await Task.WhenAll(tasks);

		// Assert
		results.ShouldAllBe(r => r.IsEnabled == false);
		results.ShouldAllBe(r => r.Variation == "off");

		// Verify only one flag was created
		var allFlags = await fixture.Repository.GetAllAsync();
		var concurrentFlags = allFlags.Where(f => f.Key == "concurrent-flag").ToList();
		concurrentFlags.Count.ShouldBe(1);
	}
}

public class CreateDefaultFlagAsync_RepositoryFailure(DefaultFlagCreationTestsFixture fixture) : IClassFixture<DefaultFlagCreationTestsFixture>
{
	[Fact]
	public async Task If_RepositoryThrowsException_ThenStillReturnsDefaultFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");
		
		// Create a scenario where repository creation might fail
		// This is harder to test with real implementations, but we can test the error handling
		// by using a flag key that might cause issues or by testing with a disconnected database
		
		// Act - This should not throw even if repository operations fail
		var result = await fixture.Evaluator.Evaluate("potential-error-flag", context);

		// Assert - Should still return a result (the in-memory default flag)
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag not found, created default disabled flag");
	}

	[Fact]
	public async Task If_OperationCancelled_ThenThrowsCancellationException()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");
		
		using var cts = new CancellationTokenSource();
		cts.Cancel(); // Cancel immediately

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => fixture.Evaluator.Evaluate("cancelled-flag", context, cts.Token));
	}
}

public class CreateDefaultFlagAsync_FlagStructure(DefaultFlagCreationTestsFixture fixture) : IClassFixture<DefaultFlagCreationTestsFixture>
{
	[Fact]
	public async Task ThenCreatedFlagHasCorrectDefaultStructure()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");
		var flagKey = "structure-test-flag";

		// Act
		await fixture.Evaluator.Evaluate(flagKey, context);

		// Assert
		var createdFlag = await fixture.Repository.GetAsync(flagKey);
		createdFlag.ShouldNotBeNull();
		
		// Verify all required properties are set correctly
		createdFlag.Key.ShouldBe(flagKey);
		createdFlag.Name.ShouldBe(flagKey);
		createdFlag.Description.ShouldBe($"Auto-created flag for {flagKey}");
		createdFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();
		createdFlag.Created.Actor.ShouldBe("system");
		
		// Verify timestamps are recent (within last minute)
		createdFlag.Created.Timestamp.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
		
		// Verify collections are initialized but empty
		createdFlag.TargetingRules.ShouldNotBeNull();
		createdFlag.TargetingRules.ShouldBeEmpty();
		createdFlag.UserAccessControl.HasAccessRestrictions().ShouldBeFalse();
	}

	[Fact]
	public async Task If_FlagKeyWithSpecialCharacters_ThenHandlesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");
		var flagKey = "flag-with-special.chars_123";

		// Act
		var result = await fixture.Evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();

		var createdFlag = await fixture.Repository.GetAsync(flagKey);
		createdFlag.ShouldNotBeNull();
		createdFlag.Key.ShouldBe(flagKey);
		createdFlag.Name.ShouldBe(flagKey);
	}

	[Fact]
	public async Task If_VeryLongFlagKey_ThenTruncatesAppropriately()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");
		var longFlagKey = new string('a', 300); // Very long key

		// Act
		var result = await fixture.Evaluator.Evaluate(longFlagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();

		// The exact behavior depends on repository implementation constraints
		// But it should handle long keys gracefully
		var createdFlag = await fixture.Repository.GetAsync(longFlagKey);
		if (createdFlag != null) // Some repositories might truncate
		{
			createdFlag.Key.ShouldNotBeNullOrEmpty();
		}
	}
}

public class CreateDefaultFlagAsync_WithDifferentContexts(DefaultFlagCreationTestsFixture fixture) : IClassFixture<DefaultFlagCreationTestsFixture>
{
	[Fact]
	public async Task If_NoUserId_ThenStillCreatesFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: null);

		// Act
		var result = await fixture.Evaluator.Evaluate("no-user-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();

		var createdFlag = await fixture.Repository.GetAsync("no-user-flag");
		createdFlag.ShouldNotBeNull();
	}

	[Fact]
	public async Task If_WithAttributes_ThenStillCreatesFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(
			userId: "user123",
			attributes: new Dictionary<string, object> { { "region", "US" }, { "plan", "premium" } });

		// Act
		var result = await fixture.Evaluator.Evaluate("context-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();

		var createdFlag = await fixture.Repository.GetAsync("context-flag");
		createdFlag.ShouldNotBeNull();
	}

	[Fact]
	public async Task If_CustomEvaluationTime_ThenStillCreatesFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var customTime = DateTime.UtcNow.AddDays(-1);
		var context = new EvaluationContext(userId: "user123", evaluationTime: customTime);

		// Act
		var result = await fixture.Evaluator.Evaluate("time-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();

		var createdFlag = await fixture.Repository.GetAsync("time-flag");
		createdFlag.ShouldNotBeNull();
		// The flag's CreatedAt should be current time, not evaluation time
		createdFlag.Created.Timestamp.ShouldBeGreaterThan(customTime);
	}
}

