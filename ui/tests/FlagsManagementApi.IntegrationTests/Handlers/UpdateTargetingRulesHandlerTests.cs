using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover UpdateTargetingRulesHandler integration scenarios:
 * Successful targeting rules creation, targeting rules removal, different operators, validation errors
 */

public class UpdateTargetingRulesHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidStringTargetingRules_ThenUpdatesTargetingRules()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("string-targeting-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var targetingRules = new List<TargetingRuleDto>
		{
			new("userId", TargetingOperator.Equals, ["user123", "user456"], "premium"),
			new("role", TargetingOperator.Contains, ["admin"], "admin-features")
		};

		var request = new UpdateTargetingRulesRequest(targetingRules, false);

		// Act
		var result = await fixture.UpdateTargetingRulesHandler.HandleAsync("string-targeting-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.Modes.ShouldContain(EvaluationMode.TargetingRules);
		okResult.Value.Modes.ShouldNotContain(EvaluationMode.Enabled);
		okResult.Value.Updated.Actor.ShouldBe("test-user");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("string-targeting-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.TargetingRules.Count.ShouldBe(2);
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TargetingRules]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeFalse();
		updatedFlag.LastModified.Actor.ShouldBe("test-user");

		// Verify rule details
		var userRule = updatedFlag.TargetingRules.First(r => r.Attribute == "userId");
		userRule.Operator.ShouldBe(TargetingOperator.Equals);
		userRule.Variation.ShouldBe("premium");
	}

	[Fact]
	public async Task If_ValidNumericTargetingRules_ThenUpdatesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("numeric-targeting-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var targetingRules = new List<TargetingRuleDto>
		{
			new("age", TargetingOperator.GreaterThan, ["18"], "adult"),
			new("score", TargetingOperator.LessThan, ["100"], "intermediate")
		};

		var request = new UpdateTargetingRulesRequest(targetingRules, false);

		// Act
		var result = await fixture.UpdateTargetingRulesHandler.HandleAsync("numeric-targeting-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.Modes.ShouldContain(EvaluationMode.TargetingRules);

		// Verify in repository - numeric rules should be created as NumericTargetingRule
		var updatedFlag = await fixture.Repository.GetAsync("numeric-targeting-flag");
		updatedFlag.TargetingRules.Count.ShouldBe(2);

		var ageRule = updatedFlag.TargetingRules.First(r => r.Attribute == "age");
		ageRule.ShouldBeOfType<NumericTargetingRule>();
		ageRule.Operator.ShouldBe(TargetingOperator.GreaterThan);
		ageRule.Variation.ShouldBe("adult");
	}
}

public class UpdateTargetingRulesHandler_ReplaceExisting(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ExistingRules_ThenReplacesWithNewRules()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("replace-rules-flag", EvaluationMode.TargetingRules);
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule("oldAttribute", TargetingOperator.Equals, ["oldValue"], "old")
		];
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.TargetingRules);
		await fixture.Repository.CreateAsync(flag);

		var newTargetingRules = new List<TargetingRuleDto>
		{
			new("newAttribute", TargetingOperator.Contains, ["newValue"], "new")
		};

		var request = new UpdateTargetingRulesRequest(newTargetingRules, false);

		// Act
		var result = await fixture.UpdateTargetingRulesHandler.HandleAsync("replace-rules-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.Modes.ShouldContain(EvaluationMode.TargetingRules);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("replace-rules-flag");
		updatedFlag.TargetingRules.Count.ShouldBe(1);
		updatedFlag.TargetingRules.First().Attribute.ShouldBe("newAttribute");
		updatedFlag.TargetingRules.First().Variation.ShouldBe("new");
	}

	[Fact]
	public async Task If_EmptyRulesList_ThenClearsTargetingRules()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("clear-rules-flag", EvaluationMode.TargetingRules);
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule("existingRule", TargetingOperator.Equals, ["value"], "variation")
		];
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.TargetingRules);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateTargetingRulesRequest([], false);

		// Act
		var result = await fixture.UpdateTargetingRulesHandler.HandleAsync("clear-rules-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.Modes.ShouldNotContain(EvaluationMode.TargetingRules);

		var updatedFlag = await fixture.Repository.GetAsync("clear-rules-flag");
		updatedFlag.TargetingRules.ShouldBeEmpty();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TargetingRules]).ShouldBeFalse();
	}
}

public class UpdateTargetingRulesHandler_RemoveRules(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_RemoveTargetingRules_ThenClearsAllRulesAndRemovesMode()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("remove-rules-flag", EvaluationMode.TargetingRules);
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule("userId", TargetingOperator.Equals, ["user123"], "premium"),
			TargetingRuleFactory.CreateTargetingRule("role", TargetingOperator.Contains, ["admin"], "admin")
		];
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.TargetingRules);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateTargetingRulesRequest(null, RemoveTargetingRules: true);

		// Act
		var result = await fixture.UpdateTargetingRulesHandler.HandleAsync("remove-rules-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.Modes.ShouldNotContain(EvaluationMode.TargetingRules);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("remove-rules-flag");
		updatedFlag.TargetingRules.ShouldBeEmpty();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TargetingRules]).ShouldBeFalse();
	}

	[Fact]
	public async Task If_RemoveFromNoRulesFlag_ThenHandlesGracefully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("no-rules-flag", EvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateTargetingRulesRequest(null, RemoveTargetingRules: true);

		// Act
		var result = await fixture.UpdateTargetingRulesHandler.HandleAsync("no-rules-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.Modes.ShouldNotContain(EvaluationMode.TargetingRules);

		var updatedFlag = await fixture.Repository.GetAsync("no-rules-flag");
		updatedFlag.TargetingRules.ShouldBeEmpty();
	}
}

public class UpdateTargetingRulesHandler_NotFound(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();
		var targetingRules = new List<TargetingRuleDto>
		{
			new("userId", TargetingOperator.Equals, ["user123"], "premium")
		};
		var request = new UpdateTargetingRulesRequest(targetingRules, false);

		// Act
		var result = await fixture.UpdateTargetingRulesHandler.HandleAsync("non-existent-flag", request, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(404);
		problemResponse.ProblemDetails.Detail.ShouldContain("non-existent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("was not found");
	}
}

public class UpdateTargetingRulesHandler_ValidationErrors(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_KeyIsEmpty_ThenReturnsBadRequest()
	{
		// Arrange
		var targetingRules = new List<TargetingRuleDto>
		{
			new("userId", TargetingOperator.Equals, ["user123"], "premium")
		};
		var request = new UpdateTargetingRulesRequest(targetingRules, false);

		// Act
		var result = await fixture.UpdateTargetingRulesHandler.HandleAsync("", request, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}
}

public class UpdateTargetingRulesHandler_CacheIntegration(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagInCache_ThenRemovesFromCacheAfterUpdate()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-targeting-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-targeting-flag", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-targeting-flag");
			cachedFlag.ShouldNotBeNull();
		}

		var targetingRules = new List<TargetingRuleDto>
		{
			new("userId", TargetingOperator.Equals, ["user123"], "premium")
		};
		var request = new UpdateTargetingRulesRequest(targetingRules, false);

		// Act
		var result = await fixture.UpdateTargetingRulesHandler.HandleAsync("cached-targeting-flag", request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify cache was cleared
		if (fixture.Cache != null)
		{
			var cachedFlagAfterUpdate = await fixture.Cache.GetAsync("cached-targeting-flag");
			cachedFlagAfterUpdate.ShouldBeNull();
		}
	}
}