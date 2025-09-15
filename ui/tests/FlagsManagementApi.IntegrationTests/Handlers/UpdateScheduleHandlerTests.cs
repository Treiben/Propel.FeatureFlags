using Azure;
using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
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
		var flag = TestHelpers.CreateTestFlag("schedule-test-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var enableDate = DateTimeOffset.UtcNow.AddHours(2);
		var disableDate = DateTimeOffset.UtcNow.AddDays(1);
		var request = new UpdateScheduleRequest(enableDate, disableDate, false);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("schedule-test-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Schedule.ShouldBe(new Propel.FlagsManagement.Api.Endpoints.Dto.ActivationSchedule(enableDate, disableDate));
		ffResponse.Modes.ShouldContain(EvaluationMode.Scheduled);
		ffResponse.Updated.Actor.ShouldBe("test-user");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("schedule-test-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Schedule.EnableOn.ShouldBeInRange(
			enableDate.DateTime.AddTicks(-10), enableDate.DateTime.AddTicks(10));
		updatedFlag.Schedule.DisableOn.Value.ShouldBeInRange(
			disableDate.DateTime.AddTicks(-10), disableDate.DateTime.AddTicks(10));
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Scheduled]).ShouldBeTrue();
		updatedFlag.LastModified.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_EnableOnlySchedule_ThenUpdatesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("enable-only-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var enableDate = DateTimeOffset.UtcNow.AddHours(3);
		var request = new UpdateScheduleRequest(enableDate, null, false);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("enable-only-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Schedule.ShouldBe(new Propel.FlagsManagement.Api.Endpoints.Dto.ActivationSchedule(enableDate, DateTimeOffset.MaxValue));
		ffResponse.Modes.ShouldContain(EvaluationMode.Scheduled);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("enable-only-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Schedule.EnableOn.ShouldBeInRange(
			enableDate.DateTime.AddTicks(-10), enableDate.DateTime.AddTicks(10));
		updatedFlag.Schedule.DisableOn.ShouldBe(DateTime.MaxValue.ToUniversalTime());
	}
}

public class UpdateScheduleHandler_RemoveSchedule(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_RemoveScheduleRequested_ThenClearsSchedule()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("scheduled-flag", EvaluationMode.Scheduled);
		flag.Schedule = Propel.FeatureFlags.Domain.ActivationSchedule.CreateSchedule(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1));
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.Scheduled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateScheduleRequest(DateTime.UtcNow.AddHours(1), null, true);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("scheduled-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Schedule.ShouldBeNull();
		ffResponse.Modes.ShouldNotContain(EvaluationMode.Scheduled);

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("scheduled-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Schedule.ShouldBe(Propel.FeatureFlags.Domain.ActivationSchedule.Unscheduled);
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Scheduled]).ShouldBeFalse();
	}

	[Fact]
	public async Task If_RemoveScheduleFromUnscheduledFlag_ThenHandlesGracefully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("unscheduled-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateScheduleRequest(DateTime.UtcNow.AddHours(1), null, true);

		// Act
		var result = await fixture.UpdateScheduleHandler.HandleAsync("unscheduled-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Schedule.ShouldBeNull();
		ffResponse.Modes.ShouldNotContain(EvaluationMode.Scheduled);

		var updatedFlag = await fixture.Repository.GetAsync("unscheduled-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Schedule.ShouldBe(Propel.FeatureFlags.Domain.ActivationSchedule.Unscheduled);
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
		var result = await fixture.UpdateScheduleHandler.HandleAsync("non-existent-flag", request, CancellationToken.None);

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
		var result = await fixture.UpdateScheduleHandler.HandleAsync("", request, CancellationToken.None);

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
		var flag = TestHelpers.CreateTestFlag("cached-schedule-flag", EvaluationMode.Disabled);
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
		var result = await fixture.UpdateScheduleHandler.HandleAsync("cached-schedule-flag", request, CancellationToken.None);

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