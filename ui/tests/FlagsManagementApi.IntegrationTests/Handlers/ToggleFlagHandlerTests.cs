using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

public class ToggleFlagHandler_EnableFlag(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_DisabledFlagExists_ThenEnablesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateGlobalFlag("enable-test-flag", EvaluationMode.Disabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Global.ToString(), null, null);
		var request = new ToggleFlagRequest(EvaluationMode: EvaluationMode.Enabled, Reason: "Integration test enable");

		// Act
		var result = await fixture.GetHandler<ToggleFlagHandler>().HandleAsync(
			flag.Key.Key, 
			headers,
			request,
			CancellationToken.None);

		// Assert
		var updateResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		updateResult.Value.ShouldNotBeNull();
		updateResult.Value.Modes.ShouldContain(EvaluationMode.Enabled);

		// Verify that flag is now fully enabled without restrictions
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		//check that modes set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();

		//check that access controls set to unrestricted
		updatedFlag.UserAccessControl.RolloutPercentage.ShouldBe(100);
		updatedFlag.TenantAccessControl.RolloutPercentage.ShouldBe(100);

		//check that schedule and operational window reset to defaults
		updatedFlag.Schedule.ShouldBe(ActivationSchedule.Unscheduled);
		updatedFlag.OperationalWindow.ShouldBe(OperationalWindow.AlwaysOpen);

		//check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_FlagAlreadyEnabled_ThenReturnsCurrentState()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateGlobalFlag("already-enabled-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Global.ToString(), null, null);
		var request = new ToggleFlagRequest(EvaluationMode: EvaluationMode.Enabled, Reason: "Integration test enable");

		// Act
		var result = await fixture.GetHandler<ToggleFlagHandler>().HandleAsync(
			flag.Key.Key,
			headers,
			request,
			CancellationToken.None);

		// Assert
		var evaluationResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		evaluationResult.Value.ShouldNotBeNull();
		evaluationResult.Value.Modes.ShouldContain(EvaluationMode.Enabled);
	}
}

public class ToggleFlagHandler_DisableFlag(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_EnabledFlagExists_ThenDisablesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateGlobalFlag("disable-test-flag", EvaluationMode.Enabled);
		flag.UserAccessControl = new AccessControl(rolloutPercentage: 75);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Global.ToString(), null, null);
		var request = new ToggleFlagRequest(EvaluationMode: EvaluationMode.Disabled, Reason: "Integration test enable");

		// Act
		var result = await fixture.GetHandler<ToggleFlagHandler>().HandleAsync(
			flag.Key.Key,
			headers,
			request,
			CancellationToken.None);


		// Assert
		var evaluationResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		evaluationResult.Value.ShouldNotBeNull();
		evaluationResult.Value.Modes.ShouldContain(EvaluationMode.Disabled);

		// Verify that flag is now fully closed to all tenants and users without restrictions
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		//check that modes set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeFalse();

		//check that access controls set to fully restricted
		updatedFlag.UserAccessControl.RolloutPercentage.ShouldBe(0);
		updatedFlag.TenantAccessControl.RolloutPercentage.ShouldBe(0);

		//check that schedule and operational window reset to defaults
		updatedFlag.Schedule.ShouldBe(ActivationSchedule.Unscheduled);
		updatedFlag.OperationalWindow.ShouldBe(OperationalWindow.AlwaysOpen);

		//check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}
}