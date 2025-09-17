using FlagsManagementApi.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Services;

public class FlagResolverService_ValidateAndResolveFlagAsync(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidRequest_ThenResolvesFlag()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("resolver-test-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);

		// Act
		var (isValid, result, resolvedFlag) = await fixture.FlagResolverService.ValidateAndResolveFlagAsync(flag.Key.Key, headers, CancellationToken.None);

		// Assert
		isValid.ShouldBeTrue();
		result.ShouldBeOfType<Ok>();
		resolvedFlag.ShouldNotBeNull();
		resolvedFlag!.Key.Key.ShouldBe(flag.Key.Key);
		resolvedFlag.Key.Scope.ShouldBe(Scope.Application);
		resolvedFlag.Key.ApplicationName.ShouldBe(flag.Key.ApplicationName);
		resolvedFlag.Key.ApplicationVersion.ShouldBe(flag.Key.ApplicationVersion);
		resolvedFlag.Name.ShouldBe(flag.Name);
		resolvedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_InvalidInputs_ThenReturnsValidationErrors()
	{
		// Arrange

		// Test empty key
		var emptyKeyHeaders = new FlagRequestHeaders("Application", "test-app", "1.0");
		var (isValid1, result1, flag1) = await fixture.FlagResolverService.ValidateAndResolveFlagAsync("", emptyKeyHeaders, CancellationToken.None);

		isValid1.ShouldBeFalse();
		flag1.ShouldBeNull();

		// Test null scope
		var nullScopeHeaders = new FlagRequestHeaders(null!, "test-app", "1.0");
		var (isValid2, result2, flag2) = await fixture.FlagResolverService.ValidateAndResolveFlagAsync("test-flag", nullScopeHeaders, CancellationToken.None);

		isValid2.ShouldBeFalse();
		flag2.ShouldBeNull();

		// Test invalid scope
		var invalidScopeHeaders = new FlagRequestHeaders("InvalidScope", "test-app", "1.0");
		var (isValid3, result3, flag3) = await fixture.FlagResolverService.ValidateAndResolveFlagAsync("test-flag", invalidScopeHeaders, CancellationToken.None);

		isValid3.ShouldBeFalse();
		flag3.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagNotFound_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), "non-existent-app", "1.0");

		// Act
		var (isValid, result, flag) = await fixture.FlagResolverService.ValidateAndResolveFlagAsync("non-existent-flag", headers, CancellationToken.None);

		// Assert
		isValid.ShouldBeFalse();
		flag.ShouldBeNull();
	}
}