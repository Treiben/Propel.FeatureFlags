using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover UpdateScheduleHandler integration scenarios:
 * Successful schedule creation, schedule removal, non-existent flags, validation errors
 */

public class UpdateScheduleHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidScheduleRequest_ThenUpdatesSchedule()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("schedule-test-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var enableDate = DateTime.UtcNow.AddHours(2);
		var disableDate = DateTime.UtcNow.AddDays(1);
		var request = new UpdateScheduleRequest(enableDate, disableDate, false);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("schedule-test-flag", request);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.ScheduledEnableDate.ShouldBe(enableDate);
		okResult.Value.ScheduledDisableDate.Value.ShouldBe(disableDate);
		okResult.Value.EvaluationModes.ShouldContain(FlagEvaluationMode.Scheduled);
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("schedule-test-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Schedule.ScheduledEnableDate.ShouldBeInRange(
			enableDate.AddTicks(-10), enableDate.AddTicks(10));
		updatedFlag.Schedule.ScheduledDisableDate.Value.ShouldBeInRange(
			disableDate.AddTicks(-10), disableDate.AddTicks(10));
		updatedFlag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Scheduled]).ShouldBeTrue();
		updatedFlag.AuditRecord.ModifiedBy.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_EnableOnlySchedule_ThenUpdatesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("enable-only-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var enableDate = DateTime.UtcNow.AddHours(3);
		var request = new UpdateScheduleRequest(enableDate, null, false);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("enable-only-flag", request);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ScheduledEnableDate.ShouldBe(enableDate);
		okResult.Value.ScheduledDisableDate.ShouldBeNull();
		okResult.Value.EvaluationModes.ShouldContain(FlagEvaluationMode.Scheduled);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("enable-only-flag");
		updatedFlag.Schedule.ScheduledEnableDate.ShouldBeInRange(
			enableDate.AddTicks(-10),
			enableDate.AddTicks(10));
		updatedFlag.Schedule.ScheduledDisableDate.ShouldBe(DateTime.MaxValue.ToUniversalTime());
	}
}

public class UpdateScheduleHandler_RemoveSchedule(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_RemoveScheduleRequested_ThenClearsSchedule()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("scheduled-flag", FlagEvaluationMode.Scheduled);
		flag.Schedule = FlagActivationSchedule.CreateSchedule(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1));
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateScheduleRequest(DateTime.UtcNow.AddHours(1), null, true);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("scheduled-flag", request);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ScheduledEnableDate.ShouldBeNull();
		okResult.Value.ScheduledDisableDate.ShouldBeNull();
		okResult.Value.EvaluationModes.ShouldNotContain(FlagEvaluationMode.Scheduled);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("scheduled-flag");
		updatedFlag.Schedule.ShouldBe(FlagActivationSchedule.Unscheduled);
		updatedFlag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Scheduled]).ShouldBeFalse();
	}

	[Fact]
	public async Task If_RemoveScheduleFromUnscheduledFlag_ThenHandlesGracefully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("unscheduled-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateScheduleRequest(DateTime.UtcNow.AddHours(1), null, true);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("unscheduled-flag", request);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ScheduledEnableDate.ShouldBeNull();
		okResult.Value.ScheduledDisableDate.ShouldBeNull();

		var updatedFlag = await fixture.Repository.GetAsync("unscheduled-flag");
		updatedFlag.Schedule.ShouldBe(FlagActivationSchedule.Unscheduled);
	}
}

public class UpdateScheduleHandler_NotFound(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();
		var enableDate = DateTime.UtcNow.AddHours(2);
		var request = new UpdateScheduleRequest(enableDate, null, false);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("non-existent-flag", request);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(404);
		problemResponse.ProblemDetails.Detail.ShouldContain("non-existent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("was not found");
	}
}

public class UpdateScheduleHandler_ValidationErrors(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_KeyIsEmpty_ThenReturnsBadRequest()
	{
		// Arrange
		var enableDate = DateTime.UtcNow.AddHours(2);
		var request = new UpdateScheduleRequest(enableDate, null, false);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("", request);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}
}

public class UpdateScheduleHandler_CacheIntegration(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagInCache_ThenRemovesFromCacheAfterScheduleUpdate()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-schedule-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-schedule-flag", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-schedule-flag");
			cachedFlag.ShouldNotBeNull();
		}

		var enableDate = DateTime.UtcNow.AddHours(2);
		var request = new UpdateScheduleRequest(enableDate, null, false);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("cached-schedule-flag", request);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify cache was cleared
		if (fixture.Cache != null)
		{
			var cachedFlagAfterUpdate = await fixture.Cache.GetAsync("cached-schedule-flag");
			cachedFlagAfterUpdate.ShouldBeNull();
		}
	}
}