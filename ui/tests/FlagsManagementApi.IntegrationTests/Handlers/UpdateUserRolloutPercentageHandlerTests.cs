using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover UpdateUserRolloutPercentageHandler integration scenarios:
 * Successful percentage updates, evaluation mode changes, non-existent flags, validation errors
 */

public class UpdateUserRolloutPercentageHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidPercentage_ThenUpdatesRolloutPercentage()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("user-rollout-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateUserRolloutPercentageRequest(75);

		// Act
		var result = await fixture.UpdateUserRolloutPercentageHandler.HandleAsync("user-rollout-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.EvaluationModes.ShouldContain(FlagEvaluationMode.UserRolloutPercentage);
		okResult.Value.UserRolloutPercentage.ShouldBe(75);
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("user-rollout-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.UserAccess.RolloutPercentage.ShouldBe(75);
		updatedFlag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.UserRolloutPercentage]).ShouldBeTrue();
		updatedFlag.AuditRecord.ModifiedBy.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_FullRollout_ThenSets100Percent()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("full-user-rollout", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateUserRolloutPercentageRequest(100);

		// Act
		var result = await fixture.UpdateUserRolloutPercentageHandler.HandleAsync("full-user-rollout", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.EvaluationModes.ShouldContain(FlagEvaluationMode.UserRolloutPercentage);
		okResult.Value.UserRolloutPercentage.ShouldBe(100);

		var updatedFlag = await fixture.Repository.GetAsync("full-user-rollout");
		updatedFlag.UserAccess.RolloutPercentage.ShouldBe(100);
		updatedFlag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.UserRolloutPercentage]).ShouldBeTrue();
	}
}

public class UpdateUserRolloutPercentageHandler_ZeroPercent(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ZeroPercent_ThenRemovesEvaluationMode()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("zero-user-rollout", FlagEvaluationMode.UserRolloutPercentage);
		flag.UserAccess = new FlagUserAccessControl(rolloutPercentage: 50);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserRolloutPercentage);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateUserRolloutPercentageRequest(0);

		// Act
		var result = await fixture.UpdateUserRolloutPercentageHandler.HandleAsync("zero-user-rollout", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.EvaluationModes.ShouldNotContain(FlagEvaluationMode.UserRolloutPercentage);
		okResult.Value.UserRolloutPercentage.ShouldBe(0);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("zero-user-rollout");
		updatedFlag.UserAccess.RolloutPercentage.ShouldBe(0);
		updatedFlag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.UserRolloutPercentage]).ShouldBeFalse();
	}
}

public class UpdateUserRolloutPercentageHandler_NotFound(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();
		var request = new UpdateUserRolloutPercentageRequest(50);

		// Act
		var result = await fixture.UpdateUserRolloutPercentageHandler.HandleAsync("non-existent-flag", request, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(404);
		problemResponse.ProblemDetails.Detail.ShouldContain("non-existent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("was not found");
	}
}

public class UpdateUserRolloutPercentageHandler_ValidationErrors(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_KeyIsEmpty_ThenReturnsBadRequest()
	{
		// Arrange
		var request = new UpdateUserRolloutPercentageRequest(50);

		// Act
		var result = await fixture.UpdateUserRolloutPercentageHandler.HandleAsync("", request, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}
}

public class UpdateUserRolloutPercentageHandler_CacheIntegration(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagInCache_ThenRemovesFromCacheAfterUpdate()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-user-rollout", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-user-rollout", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-user-rollout");
			cachedFlag.ShouldNotBeNull();
		}

		var request = new UpdateUserRolloutPercentageRequest(60);

		// Act
		var result = await fixture.UpdateUserRolloutPercentageHandler.HandleAsync("cached-user-rollout", request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify cache was cleared
		if (fixture.Cache != null)
		{
			var cachedFlagAfterUpdate = await fixture.Cache.GetAsync("cached-user-rollout");
			cachedFlagAfterUpdate.ShouldBeNull();
		}
	}
}