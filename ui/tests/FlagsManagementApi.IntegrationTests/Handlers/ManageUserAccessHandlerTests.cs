using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

public class ManageUserAccessHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidUserAccessRequest_ThenUpdatesUserAccess()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("user-access-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new ManageUserAccessRequest(
			AllowedUsers: ["user1", "user2"],
			BlockedUsers: ["blocked-user"],
			Percentage: 85);

		// Act
		var result = await fixture.GetHandler<ManageUserAccessHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var updateResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var ffResponse = updateResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Modes.ShouldContain(EvaluationMode.UserRolloutPercentage);
		ffResponse.Modes.ShouldContain(EvaluationMode.UserTargeted);
		ffResponse.Modes.ShouldNotContain(EvaluationMode.Enabled);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);
		
		updatedFlag.ShouldNotBeNull();

		// Check that modes set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserRolloutPercentage]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserTargeted]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeFalse();

		// Check that user access control set correctly
		updatedFlag.UserAccessControl.Allowed.ShouldContain("user1");
		updatedFlag.UserAccessControl.Allowed.ShouldContain("user2");
		updatedFlag.UserAccessControl.Blocked.ShouldContain("blocked-user");
		updatedFlag.UserAccessControl.RolloutPercentage.ShouldBe(85);

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_ZeroPercentageRequest_ThenRemovesUserRolloutMode()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("zero-percentage-flag", EvaluationMode.UserRolloutPercentage);
		flag.UserAccessControl = new AccessControl(rolloutPercentage: 50);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new ManageUserAccessRequest(
			AllowedUsers: null,
			BlockedUsers: null,
			Percentage: 0);

		// Act
		var result = await fixture.GetHandler<ManageUserAccessHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Modes.ShouldNotContain(EvaluationMode.UserRolloutPercentage);
		ffResponse.Modes.ShouldNotContain(EvaluationMode.UserTargeted);
		ffResponse.Modes.ShouldContain(EvaluationMode.Disabled);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that modes set correctly - 0% effectively disables the flag
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserRolloutPercentage]).ShouldBeFalse();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserTargeted]).ShouldBeFalse();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();

		// Check that user access control updated
		updatedFlag.UserAccessControl.RolloutPercentage.ShouldBe(0);
		updatedFlag.UserAccessControl.Allowed.ShouldBeEmpty();
		updatedFlag.UserAccessControl.Blocked.ShouldBeEmpty();

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_OnlyPercentageProvided_ThenUpdatesUserRolloutOnly()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("percentage-only-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new ManageUserAccessRequest(
			AllowedUsers: null,
			BlockedUsers: null,
			Percentage: 60);

		// Act
		var result = await fixture.GetHandler<ManageUserAccessHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Modes.ShouldContain(EvaluationMode.UserRolloutPercentage);
		ffResponse.Modes.ShouldNotContain(EvaluationMode.UserTargeted);
		ffResponse.Modes.ShouldNotContain(EvaluationMode.Enabled);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that modes set correctly - only percentage rollout, no targeting
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserRolloutPercentage]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserTargeted]).ShouldBeFalse();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeFalse();

		// Check that user access control updated correctly
		updatedFlag.UserAccessControl.RolloutPercentage.ShouldBe(60);
		updatedFlag.UserAccessControl.Allowed.ShouldBeEmpty();
		updatedFlag.UserAccessControl.Blocked.ShouldBeEmpty();

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}
}