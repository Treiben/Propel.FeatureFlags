using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover UpdateTenantRolloutPercentageHandler integration scenarios:
 * Successful percentage updates, evaluation mode changes, non-existent flags, validation errors
 */

public class UpdateTenantRolloutPercentageHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidPercentage_ThenUpdatesRolloutPercentage()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("tenant-rollout-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateTenantRolloutPercentageRequest(75);

		// Act
		var result = await fixture.UpdateTenantRolloutPercentageHandler.HandleAsync("tenant-rollout-flag", request);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.EvaluationModes.ShouldContain(FlagEvaluationMode.TenantRolloutPercentage);
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("tenant-rollout-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.TenantAccess.RolloutPercentage.ShouldBe(75);
		updatedFlag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.TenantRolloutPercentage]).ShouldBeTrue();
		updatedFlag.AuditRecord.ModifiedBy.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_FullRollout_ThenSets100Percent()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("full-tenant-rollout", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateTenantRolloutPercentageRequest(100);

		// Act
		var result = await fixture.UpdateTenantRolloutPercentageHandler.HandleAsync("full-tenant-rollout", request);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.EvaluationModes.ShouldContain(FlagEvaluationMode.TenantRolloutPercentage);

		var updatedFlag = await fixture.Repository.GetAsync("full-tenant-rollout");
		updatedFlag.TenantAccess.RolloutPercentage.ShouldBe(100);
		updatedFlag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.TenantRolloutPercentage]).ShouldBeTrue();
	}
}

public class UpdateTenantRolloutPercentageHandler_ZeroPercent(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ZeroPercent_ThenRemovesEvaluationMode()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("zero-tenant-rollout", FlagEvaluationMode.TenantRolloutPercentage);
		flag.TenantAccess = new FlagTenantAccessControl(rolloutPercentage: 50);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TenantRolloutPercentage);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateTenantRolloutPercentageRequest(0);

		// Act
		var result = await fixture.UpdateTenantRolloutPercentageHandler.HandleAsync("zero-tenant-rollout", request);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.EvaluationModes.ShouldNotContain(FlagEvaluationMode.TenantRolloutPercentage);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("zero-tenant-rollout");
		updatedFlag.TenantAccess.RolloutPercentage.ShouldBe(0);
		updatedFlag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.TenantRolloutPercentage]).ShouldBeFalse();
	}
}

public class UpdateTenantRolloutPercentageHandler_NotFound(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();
		var request = new UpdateTenantRolloutPercentageRequest(50);

		// Act
		var result = await fixture.UpdateTenantRolloutPercentageHandler.HandleAsync("non-existent-flag", request);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(404);
		problemResponse.ProblemDetails.Detail.ShouldContain("non-existent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("was not found");
	}
}

public class UpdateTenantRolloutPercentageHandler_ValidationErrors(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_KeyIsEmpty_ThenReturnsBadRequest()
	{
		// Arrange
		var request = new UpdateTenantRolloutPercentageRequest(50);

		// Act
		var result = await fixture.UpdateTenantRolloutPercentageHandler.HandleAsync("", request);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}
}

public class UpdateTenantRolloutPercentageHandler_CacheIntegration(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagInCache_ThenRemovesFromCacheAfterUpdate()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-tenant-rollout", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-tenant-rollout", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-tenant-rollout");
			cachedFlag.ShouldNotBeNull();
		}

		var request = new UpdateTenantRolloutPercentageRequest(60);

		// Act
		var result = await fixture.UpdateTenantRolloutPercentageHandler.HandleAsync("cached-tenant-rollout", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify cache was cleared
		if (fixture.Cache != null)
		{
			var cachedFlagAfterUpdate = await fixture.Cache.GetAsync("cached-tenant-rollout");
			cachedFlagAfterUpdate.ShouldBeNull();
		}
	}
}