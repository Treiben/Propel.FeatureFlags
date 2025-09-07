using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover ToggleFlagHandler integration scenarios:
 * Enabling flags, disabling flags, non-existent flags, cache integration, audit tracking
 */


public class ToggleFlagHandler_EnableFlag(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_DisabledFlagExists_ThenEnablesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("enable-test-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("enable-test-flag", FlagEvaluationMode.Enabled, "Integration test enable");

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.EvaluationModes.ShouldContain(FlagEvaluationMode.Enabled);
		okResult.Value.PercentageEnabled.ShouldBe(100);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("enable-test-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled]).ShouldBeTrue();
		updatedFlag.UserAccess.RolloutPercentage.ShouldBe(100);
		updatedFlag.AuditRecord.ModifiedBy.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_FlagAlreadyEnabled_ThenReturnsCurrentState()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("already-enabled-flag", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("already-enabled-flag", FlagEvaluationMode.Enabled, "Already enabled test");

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.EvaluationModes.ShouldContain(FlagEvaluationMode.Enabled);
	}
}

public class ToggleFlagHandler_DisableFlag(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_EnabledFlagExists_ThenDisablesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("disable-test-flag", FlagEvaluationMode.Enabled);
		flag.UserAccess = new FlagUserAccessControl(rolloutPercentage: 75);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("disable-test-flag", FlagEvaluationMode.Disabled, "Integration test disable");

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.EvaluationModes.ShouldContain(FlagEvaluationMode.Disabled);
		okResult.Value.PercentageEnabled.ShouldBe(0);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("disable-test-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Disabled]).ShouldBeTrue();
		updatedFlag.UserAccess.RolloutPercentage.ShouldBe(0);
		updatedFlag.Schedule.ShouldBe(FlagActivationSchedule.Unscheduled);
		updatedFlag.OperationalWindow.ShouldBe(FlagOperationalWindow.AlwaysOpen);
	}

	[Fact]
	public async Task If_FlagAlreadyDisabled_ThenReturnsCurrentState()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("already-disabled-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("already-disabled-flag", FlagEvaluationMode.Disabled, "Already disabled test");

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.EvaluationModes.ShouldContain(FlagEvaluationMode.Disabled);
	}
}

public class ToggleFlagHandler_NotFound(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("non-existent-flag", FlagEvaluationMode.Enabled, "Test enable");

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(404);
		problemResponse.ProblemDetails.Detail.ShouldContain("non-existent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("was not found");
	}
}

public class ToggleFlagHandler_CacheIntegration(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagInCache_ThenRemovesFromCacheAfterToggle()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-toggle-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-toggle-flag", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-toggle-flag");
			cachedFlag.ShouldNotBeNull();
		}

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("cached-toggle-flag", FlagEvaluationMode.Enabled, "Cache integration test");

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify cache was cleared
		if (fixture.Cache != null)
		{
			var cachedFlagAfterToggle = await fixture.Cache.GetAsync("cached-toggle-flag");
			cachedFlagAfterToggle.ShouldBeNull();
		}
	}
}

public class ToggleFlagHandler_ValidationErrors(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_KeyIsEmpty_ThenReturnsBadRequest()
	{
		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("", FlagEvaluationMode.Enabled, "Test reason");

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}
}