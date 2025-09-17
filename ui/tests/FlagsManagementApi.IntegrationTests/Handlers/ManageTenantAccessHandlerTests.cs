using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

public class ManageTenantAccessHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidTenantAccessRequest_ThenUpdatesTenantAccess()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("tenant-access-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new ManageTenantAccessRequest(
			AllowedTenants: ["tenant1", "tenant2"],
			BlockedTenants: ["blocked-tenant"],
			Percentage: 75);

		// Act
		var result = await fixture.GetHandler<ManageTenantAccessHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var updateResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var ffResponse = updateResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Modes.ShouldContain(EvaluationMode.TenantRolloutPercentage);
		ffResponse.Modes.ShouldContain(EvaluationMode.TenantTargeted);
		ffResponse.Modes.ShouldNotContain(EvaluationMode.Enabled);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that modes set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TenantRolloutPercentage]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TenantTargeted]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeFalse();

		// Check that tenant access control set correctly
		updatedFlag.TenantAccessControl.Allowed.ShouldContain("tenant1");
		updatedFlag.TenantAccessControl.Allowed.ShouldContain("tenant2");
		updatedFlag.TenantAccessControl.Blocked.ShouldContain("blocked-tenant");
		updatedFlag.TenantAccessControl.RolloutPercentage.ShouldBe(75);

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_ZeroPercentageRequest_ThenRemovesTenantRolloutMode()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("zero-percentage-flag", EvaluationMode.TenantRolloutPercentage);
		flag.TenantAccessControl = new AccessControl(rolloutPercentage: 50);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new ManageTenantAccessRequest(
			AllowedTenants: null,
			BlockedTenants: null,
			Percentage: 0);

		// Act
		var result = await fixture.GetHandler<ManageTenantAccessHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Modes.ShouldNotContain(EvaluationMode.TenantRolloutPercentage);
		ffResponse.Modes.ShouldNotContain(EvaluationMode.TenantTargeted);
		ffResponse.Modes.ShouldContain(EvaluationMode.Disabled);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that modes set correctly - 0% effectively disables the flag
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TenantRolloutPercentage]).ShouldBeFalse();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TenantTargeted]).ShouldBeFalse();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();

		// Check that tenant access control updated
		updatedFlag.TenantAccessControl.RolloutPercentage.ShouldBe(0);
		updatedFlag.TenantAccessControl.Allowed.ShouldBeEmpty();
		updatedFlag.TenantAccessControl.Blocked.ShouldBeEmpty();

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_OnlyPercentageProvided_ThenUpdatesTenantRolloutOnly()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("percentage-only-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new ManageTenantAccessRequest(
			AllowedTenants: null,
			BlockedTenants: null,
			Percentage: 45);

		// Act
		var result = await fixture.GetHandler<ManageTenantAccessHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Modes.ShouldContain(EvaluationMode.TenantRolloutPercentage);
		ffResponse.Modes.ShouldNotContain(EvaluationMode.TenantTargeted);
		ffResponse.Modes.ShouldNotContain(EvaluationMode.Enabled);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that modes set correctly - only percentage rollout, no targeting
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TenantRolloutPercentage]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TenantTargeted]).ShouldBeFalse();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeFalse();

		// Check that tenant access control updated correctly
		updatedFlag.TenantAccessControl.RolloutPercentage.ShouldBe(45);
		updatedFlag.TenantAccessControl.Allowed.ShouldBeEmpty();
		updatedFlag.TenantAccessControl.Blocked.ShouldBeEmpty();

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}
}