using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover UpdateTimeWindowHandler integration scenarios:
 * Successful time window creation, time window removal, different time zones, validation errors
 */

public class UpdateTimeWindowHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidTimeWindow_ThenUpdatesOperationalWindow()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("time-window-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var startTime = new TimeOnly(9, 0); // 9:00 AM
		var endTime = new TimeOnly(17, 0);  // 5:00 PM
		var timeZone = "UTC";
		var windowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };

		var request = new UpdateTimeWindowRequest(startTime, endTime, timeZone, windowDays, false);

		// Act
		var result = await fixture.UpdateTimeWindowHandler.HandleAsync("time-window-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.TimeWindow.StartOn.ShouldBe(startTime);
		okResult.Value.TimeWindow.StopOn.ShouldBe(endTime);
		okResult.Value.TimeWindow.TimeZone.ShouldBe(timeZone);
		okResult.Value.TimeWindow.DaysActive.ShouldBe(windowDays.ToArray());
		okResult.Value.Updated.Actor.ShouldBe("test-user");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("time-window-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.OperationalWindow.StartOn.ShouldBe(startTime.ToTimeSpan());
		updatedFlag.OperationalWindow.StopOn.ShouldBe(endTime.ToTimeSpan());
		updatedFlag.OperationalWindow.TimeZone.ShouldBe(timeZone);
		updatedFlag.OperationalWindow.DaysActive.ShouldBe(windowDays.ToArray());
		updatedFlag.LastModified.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_DifferentTimeZone_ThenUpdatesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("timezone-window-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var startTime = new TimeOnly(8, 30);
		var endTime = new TimeOnly(18, 30);
		var timeZone = "America/New_York";
		var windowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday };

		var request = new UpdateTimeWindowRequest(startTime, endTime, timeZone, windowDays, false);

		// Act
		var result = await fixture.UpdateTimeWindowHandler.HandleAsync("timezone-window-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.TimeWindow.TimeZone.ShouldBe(timeZone);
		okResult.Value.TimeWindow.DaysActive.ShouldBe(windowDays.ToArray());

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("timezone-window-flag");
		updatedFlag.OperationalWindow.TimeZone.ShouldBe(timeZone);
		updatedFlag.OperationalWindow.DaysActive.ShouldBe(windowDays.ToArray());
	}
}

public class UpdateTimeWindowHandler_WeekendWindow(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_WeekendOnly_ThenSetsCorrectDays()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("weekend-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var startTime = new TimeOnly(10, 0);
		var endTime = new TimeOnly(22, 0);
		var timeZone = "UTC";
		var weekendDays = new List<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };

		var request = new UpdateTimeWindowRequest(startTime, endTime, timeZone, weekendDays, false);

		// Act
		var result = await fixture.UpdateTimeWindowHandler.HandleAsync("weekend-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.TimeWindow.DaysActive.ShouldContain(DayOfWeek.Saturday);
		okResult.Value.TimeWindow.DaysActive.ShouldContain(DayOfWeek.Sunday);
		okResult.Value.TimeWindow.DaysActive.Length.ShouldBe(2);

		var updatedFlag = await fixture.Repository.GetAsync("weekend-flag");
		updatedFlag.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Saturday);
		updatedFlag.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Sunday);
	}
}

public class UpdateTimeWindowHandler_RemoveWindow(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_RemoveTimeWindow_ThenSetsToAlwaysOpen()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("windowed-flag", EvaluationMode.TimeWindow);
		flag.OperationalWindow = Propel.FeatureFlags.Core.OperationalWindow.CreateWindow(
			new TimeOnly(9, 0).ToTimeSpan(),
			new TimeOnly(17, 0).ToTimeSpan(),
			"UTC",
			[DayOfWeek.Monday, DayOfWeek.Friday]);
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.TimeWindow);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateTimeWindowRequest(new TimeOnly(0, 0), new TimeOnly(0, 0), "", [], RemoveTimeWindow: true);

		// Act
		var result = await fixture.UpdateTimeWindowHandler.HandleAsync("windowed-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.TimeWindow.ShouldBeNull();

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("windowed-flag");
		updatedFlag.OperationalWindow.ShouldBe(Propel.FeatureFlags.Core.OperationalWindow.AlwaysOpen);
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TimeWindow]).ShouldBeFalse();
	}

	[Fact]
	public async Task If_RemoveFromAlwaysOpenFlag_ThenHandlesGracefully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("always-open-flag", EvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateTimeWindowRequest(new TimeOnly(0, 0), new TimeOnly(0, 0), "", [], RemoveTimeWindow: true);

		// Act
		var result = await fixture.UpdateTimeWindowHandler.HandleAsync("always-open-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.TimeWindow.ShouldBeNull();

		var updatedFlag = await fixture.Repository.GetAsync("always-open-flag");
		updatedFlag.OperationalWindow.ShouldBe(Propel.FeatureFlags.Core.OperationalWindow.AlwaysOpen);
	}
}

public class UpdateTimeWindowHandler_NotFound(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();
		var request = new UpdateTimeWindowRequest(new TimeOnly(9, 0), new TimeOnly(17, 0), "UTC", [DayOfWeek.Monday], false);

		// Act
		var result = await fixture.UpdateTimeWindowHandler.HandleAsync("non-existent-flag", request, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(404);
		problemResponse.ProblemDetails.Detail.ShouldContain("non-existent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("was not found");
	}
}

public class UpdateTimeWindowHandler_ValidationErrors(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_KeyIsEmpty_ThenReturnsBadRequest()
	{
		// Arrange
		var request = new UpdateTimeWindowRequest(new TimeOnly(9, 0), new TimeOnly(17, 0), "UTC", [DayOfWeek.Monday], false);

		// Act
		var result = await fixture.UpdateTimeWindowHandler.HandleAsync("", request, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}
}

public class UpdateTimeWindowHandler_CacheIntegration(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagInCache_ThenRemovesFromCacheAfterUpdate()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-window-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-window-flag", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-window-flag");
			cachedFlag.ShouldNotBeNull();
		}

		var request = new UpdateTimeWindowRequest(new TimeOnly(10, 0), new TimeOnly(16, 0), "UTC", [DayOfWeek.Wednesday], false);

		// Act
		var result = await fixture.UpdateTimeWindowHandler.HandleAsync("cached-window-flag", request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify cache was cleared
		if (fixture.Cache != null)
		{
			var cachedFlagAfterUpdate = await fixture.Cache.GetAsync("cached-window-flag");
			cachedFlagAfterUpdate.ShouldBeNull();
		}
	}
}