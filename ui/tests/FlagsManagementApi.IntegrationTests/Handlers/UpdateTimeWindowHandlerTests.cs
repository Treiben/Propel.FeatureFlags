using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

public class UpdateTimeWindowHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidTimeWindow_ThenUpdatesOperationalWindow()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("time-window-flag", EvaluationMode.Disabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var startTime = new TimeOnly(9, 0); // 9:00 AM
		var endTime = new TimeOnly(17, 0);  // 5:00 PM
		var timeZone = "UTC";
		var windowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateTimeWindowRequest(startTime, endTime, timeZone, windowDays, false);

		// Act
		var result = await fixture.GetHandler<UpdateTimeWindowHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();

		var resultData = okResult.Value;
		resultData.TimeWindow.ShouldNotBeNull();
		resultData.TimeWindow.StartOn.ShouldBe(startTime);
		resultData.TimeWindow.StopOn.ShouldBe(endTime);
		resultData.TimeWindow.TimeZone.ShouldBe(timeZone);
		resultData.TimeWindow.DaysActive.ShouldBe(windowDays.ToArray());

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that modes set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TimeWindow]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeFalse();

		// Check that operational window set correctly
		updatedFlag.OperationalWindow.StartOn.ShouldBe(startTime.ToTimeSpan());
		updatedFlag.OperationalWindow.StopOn.ShouldBe(endTime.ToTimeSpan());
		updatedFlag.OperationalWindow.TimeZone.ShouldBe(timeZone);
		updatedFlag.OperationalWindow.DaysActive.ShouldBe([.. windowDays]);

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_DifferentTimeZone_ThenUpdatesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("timezone-window-flag", EvaluationMode.Disabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var startTime = new TimeOnly(8, 30);
		var endTime = new TimeOnly(18, 30);
		var timeZone = "America/New_York";
		var windowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday };

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateTimeWindowRequest(startTime, endTime, timeZone, windowDays, false);

		// Act
		var result = await fixture.GetHandler<UpdateTimeWindowHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var resultData = okResult.Value;
		resultData.ShouldNotBeNull();
		resultData.TimeWindow.ShouldNotBeNull();
		resultData.TimeWindow.TimeZone.ShouldBe(timeZone);
		resultData.TimeWindow.DaysActive.ShouldBe(windowDays.ToArray());

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that modes set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TimeWindow]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeFalse();

		// Check that operational window set correctly
		updatedFlag.OperationalWindow.StartOn.ShouldBe(startTime.ToTimeSpan());
		updatedFlag.OperationalWindow.StopOn.ShouldBe(endTime.ToTimeSpan());
		updatedFlag.OperationalWindow.TimeZone.ShouldBe(timeZone);
		updatedFlag.OperationalWindow.DaysActive.ShouldBe([.. windowDays]);

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified.Actor.ShouldBe("test-user");
	}
}

public class UpdateTimeWindowHandler_WeekendWindow(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_WeekendOnly_ThenSetsCorrectDays()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateGlobalFlag("weekend-flag", EvaluationMode.Disabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var startTime = new TimeOnly(10, 0);
		var endTime = new TimeOnly(22, 0);
		var timeZone = "UTC";
		var weekendDays = new List<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };

		var headers = new FlagRequestHeaders(Scope.Global.ToString(), null, null);
		var request = new UpdateTimeWindowRequest(startTime, endTime, timeZone, weekendDays, false);

		// Act
		var result = await fixture.GetHandler<UpdateTimeWindowHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var resultData = okResult.Value;
		resultData.ShouldNotBeNull();
		resultData.TimeWindow.ShouldNotBeNull();
		resultData.TimeWindow.DaysActive.ShouldNotBeNull();
		resultData.TimeWindow.DaysActive.ShouldContain(DayOfWeek.Saturday);
		resultData.TimeWindow.DaysActive.ShouldContain(DayOfWeek.Sunday);
		resultData.TimeWindow.DaysActive.Length.ShouldBe(2);

		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that modes set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TimeWindow]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeFalse();

		// Check that operational window set correctly
		updatedFlag.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Saturday);
		updatedFlag.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Sunday);
		updatedFlag.OperationalWindow.StartOn.ShouldBe(startTime.ToTimeSpan());
		updatedFlag.OperationalWindow.StopOn.ShouldBe(endTime.ToTimeSpan());

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified.Actor.ShouldBe("test-user");
	}
}

public class UpdateTimeWindowHandler_RemoveWindow(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_RemoveTimeWindow_ThenSetsToAlwaysOpen()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("windowed-flag", EvaluationMode.TimeWindow);
		flag.OperationalWindow = new OperationalWindow(
			new TimeOnly(9, 0).ToTimeSpan(),
			new TimeOnly(17, 0).ToTimeSpan(),
			"UTC",
			[DayOfWeek.Monday, DayOfWeek.Friday]);
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.TimeWindow);

		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateTimeWindowRequest(new TimeOnly(0, 0), new TimeOnly(0, 0), "", [], RemoveTimeWindow: true);

		// Act
		var result = await fixture.GetHandler<UpdateTimeWindowHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var resultData = okResult.Value;
		resultData.ShouldNotBeNull();
		resultData.TimeWindow.ShouldBeNull();

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that operational window set correctly
		updatedFlag.OperationalWindow.ShouldBe(OperationalWindow.AlwaysOpen);

		// check operational modes
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TimeWindow]).ShouldBeFalse();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();

		//Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified.Actor.ShouldBe("test-user");
	}
}