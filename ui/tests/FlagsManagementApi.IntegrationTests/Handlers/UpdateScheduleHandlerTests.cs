using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

public class UpdateScheduleHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidScheduleRequest_ThenUpdatesSchedule()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("schedule-test-flag", EvaluationMode.Disabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var enableDate = DateTimeOffset.UtcNow.AddHours(2);
		var disableDate = DateTimeOffset.UtcNow.AddDays(1);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateScheduleRequest(enableDate, disableDate);

		// Act
		var result = await fixture.GetHandler<UpdateScheduleHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var updateResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var ffResponse = updateResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Schedule.ShouldBe(new FlagSchedule(enableDate, disableDate));
		ffResponse.Modes.ShouldContain(EvaluationMode.Scheduled);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);
		
		updatedFlag.ShouldNotBeNull();

		//check that modes set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Scheduled]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeFalse();

		//check that schedule set correctly (with some leeway for time passed during test execution)
		updatedFlag.Schedule.EnableOn.ShouldBeInRange(
			enableDate.DateTime.AddTicks(-10), enableDate.DateTime.AddTicks(10));
		updatedFlag.Schedule.DisableOn.ShouldBeInRange(
			disableDate.DateTime.AddTicks(-10), disableDate.DateTime.AddTicks(10));

		//check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_EnableOnlySchedule_ThenUpdatesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("enable-only-flag", EvaluationMode.Disabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var enableDate = DateTimeOffset.UtcNow.AddHours(3);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateScheduleRequest(enableDate, null);

		// Act
		var result = await fixture.GetHandler<UpdateScheduleHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Schedule.ShouldBe(new Propel.FlagsManagement.Api.Endpoints.Dto.FlagSchedule(enableDate, DateTimeOffset.MaxValue));
		ffResponse.Modes.ShouldContain(EvaluationMode.Scheduled);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		//check that modes set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Scheduled]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeFalse();

		//check that schedule set correctly (with some leeway for time passed during test execution)
		updatedFlag.Schedule.EnableOn.ShouldBeInRange(
			enableDate.DateTime.AddTicks(-10), enableDate.DateTime.AddTicks(10));
		updatedFlag.Schedule.DisableOn.ShouldBe(DateTime.MaxValue.ToUniversalTime());

		//check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}
}

public class UpdateScheduleHandler_RemoveSchedule(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_RemoveScheduleRequested_ThenClearsSchedule()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("scheduled-flag", EvaluationMode.Scheduled);
		flag.Schedule = ActivationSchedule.CreateSchedule(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1));
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.Scheduled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateScheduleRequest(null, null);

		// Act
		var result = await fixture.GetHandler<UpdateScheduleHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Schedule.ShouldBeNull();
		ffResponse.Modes.ShouldNotContain(EvaluationMode.Scheduled);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		//check that modes set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Scheduled]).ShouldBeFalse();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();

		//check that schedule set correctly (with some leeway for time passed during test execution)
		updatedFlag.Schedule.ShouldBe(ActivationSchedule.Unscheduled);

		//check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}
}