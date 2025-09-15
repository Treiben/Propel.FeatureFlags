using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
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
		var flag = TestHelpers.CreateTestFlag("enable-test-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("enable-test-flag", EvaluationMode.Enabled, "Integration test enable", CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.Modes.ShouldContain(EvaluationMode.Enabled);
		okResult.Value.UserAccess.RolloutPercentage.ShouldBe(100);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("enable-test-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();
		updatedFlag.UserAccessControl.RolloutPercentage.ShouldBe(100);
		updatedFlag.LastModified.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_FlagAlreadyEnabled_ThenReturnsCurrentState()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("already-enabled-flag", EvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("already-enabled-flag", EvaluationMode.Enabled, "Already enabled test", CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.Modes.ShouldContain(EvaluationMode.Enabled);
	}
}

public class ToggleFlagHandler_DisableFlag(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_EnabledFlagExists_ThenDisablesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("disable-test-flag", EvaluationMode.Enabled);
		flag.UserAccessControl = new AccessControl(rolloutPercentage: 75);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("disable-test-flag", EvaluationMode.Disabled, "Integration test disable", CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.Modes.ShouldContain(EvaluationMode.Disabled);
		okResult.Value.UserAccess.RolloutPercentage.ShouldBe(0);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("disable-test-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();
		updatedFlag.UserAccessControl.RolloutPercentage.ShouldBe(0);
		updatedFlag.Schedule.ShouldBe(Propel.FeatureFlags.Domain.ActivationSchedule.Unscheduled);
		updatedFlag.OperationalWindow.ShouldBe(Propel.FeatureFlags.Domain.OperationalWindow.AlwaysOpen);
	}

	[Fact]
	public async Task If_FlagAlreadyDisabled_ThenReturnsCurrentState()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("already-disabled-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("already-disabled-flag", EvaluationMode.Disabled, "Already disabled test", CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.Modes.ShouldContain(EvaluationMode.Disabled);
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
		var result = await fixture.ToggleFlagHandler.HandleAsync("non-existent-flag", EvaluationMode.Enabled, "Test enable", CancellationToken.None);

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
		var flag = TestHelpers.CreateTestFlag("cached-toggle-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-toggle-flag", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-toggle-flag");
			cachedFlag.ShouldNotBeNull();
		}

		// Act
		var result = await fixture.ToggleFlagHandler.HandleAsync("cached-toggle-flag", EvaluationMode.Enabled, "Cache integration test", CancellationToken.None);

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
		var result = await fixture.ToggleFlagHandler.HandleAsync("", EvaluationMode.Enabled, "Test reason", CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}
}