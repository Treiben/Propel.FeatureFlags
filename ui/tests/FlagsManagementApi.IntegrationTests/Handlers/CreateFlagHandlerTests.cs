using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover CreateFlagHandler integration scenarios:
 * Creating new flags, duplicate key validation, request validation, repository integration
 */
public class CreateFlagHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidRequest_ThenCreatesFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var request = new CreateFeatureFlagRequest
		{
			Key = "test-flag-1",
			Name = "Test Flag 1",
			Description = "A test flag",
			IsPermanent = false
		};

		// Act
		var result = await fixture.CreateFlagHandler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		var createdResponse = result.ShouldBeOfType<Created<FeatureFlagResponse>>();
		createdResponse.Location.ShouldBe("/api/flags/test-flag-1");
		createdResponse.Value.ShouldNotBeNull();
		createdResponse.Value.Key.ShouldBe("test-flag-1");
		createdResponse.Value.Name.ShouldBe("Test Flag 1");
		createdResponse.Value.Description.ShouldBe("A test flag");
		createdResponse.Value.Created.Actor.ShouldBe("test-user");

		// Verify flag was created in repository
		var createdFlag = await fixture.Repository.GetAsync("test-flag-1");
		createdFlag.ShouldNotBeNull();
		createdFlag.Key.ShouldBe("test-flag-1");
		createdFlag.Name.ShouldBe("Test Flag 1");
		createdFlag.Created.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_RequestWithTags_ThenCreatesWithTags()
	{
		// Arrange
		await fixture.ClearAllData();
		var request = new CreateFeatureFlagRequest
		{
			Key = "tagged-flag",
			Name = "Tagged Flag",
			Tags = new Dictionary<string, string> { { "team", "platform" }, { "env", "test" } }
		};

		// Act
		var result = await fixture.CreateFlagHandler.HandleAsync(request, CancellationToken.None);

		// Assert
		var createdResponse = result.ShouldBeOfType<Created<FeatureFlagResponse>>();
		createdResponse.Value.Tags.ShouldContainKeyAndValue("team", "platform");
		createdResponse.Value.Tags.ShouldContainKeyAndValue("env", "test");

		var createdFlag = await fixture.Repository.GetAsync("tagged-flag");
		createdFlag.Tags.ShouldContainKeyAndValue("team", "platform");
	}
}

public class CreateFlagHandler_Conflict(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagKeyExists_ThenReturnsConflict()
	{
		// Arrange
		await fixture.ClearAllData();
		var existingFlag = TestHelpers.CreateTestFlag("duplicate-key", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(existingFlag);

		var request = new CreateFeatureFlagRequest
		{
			Key = "duplicate-key",
			Name = "Duplicate Flag"
		};

		// Act
		var result = await fixture.CreateFlagHandler.HandleAsync(request, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(409);
		problemResponse.ProblemDetails.Detail.ShouldContain("duplicate-key");
		problemResponse.ProblemDetails.Detail.ShouldContain("already exists");
	}
}

public class CreateFlagHandler_WithLifecycle(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_PermanentFlag_ThenSetsLifecycleCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var request = new CreateFeatureFlagRequest
		{
			Key = "permanent-flag",
			Name = "Permanent Flag",
			IsPermanent = true
		};

		// Act
		var result = await fixture.CreateFlagHandler.HandleAsync(request, CancellationToken.None);

		// Assert
		var createdResponse = result.ShouldBeOfType<Created<FeatureFlagResponse>>();
		createdResponse.Value.IsPermanent.ShouldBeTrue();

		var createdFlag = await fixture.Repository.GetAsync("permanent-flag");
		createdFlag.Lifecycle.IsPermanent.ShouldBeTrue();
	}

	[Fact]
	public async Task If_ExpirationDate_ThenSetsLifecycleCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var expirationDate = DateTime.UtcNow.AddDays(30);
		var request = new CreateFeatureFlagRequest
		{
			Key = "expiring-flag",
			Name = "Expiring Flag",
			ExpirationDate = expirationDate
		};

		// Act
		var result = await fixture.CreateFlagHandler.HandleAsync(request, CancellationToken.None);

		// Assert
		var createdResponse = result.ShouldBeOfType<Created<FeatureFlagResponse>>();
		createdResponse.Value.ExpirationDate.ShouldBe(expirationDate);

		var createdFlag = await fixture.Repository.GetAsync("expiring-flag");
		createdFlag.Lifecycle.ExpirationDate.Value.ShouldBeInRange(
			expirationDate.AddTicks(-10),
			expirationDate.AddTicks(10));
	}
}

public class CreateFlagHandler_DefaultValues(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task ThenSetsCorrectDefaults()
	{
		// Arrange
		await fixture.ClearAllData();
		var request = new CreateFeatureFlagRequest
		{
			Key = "default-flag",
			Name = "Default Flag"
		};

		// Act
		var result = await fixture.CreateFlagHandler.HandleAsync(request, CancellationToken.None);

		// Assert
		var createdResponse = result.ShouldBeOfType<Created<FeatureFlagResponse>>();
		createdResponse.Value.Description.ShouldBe(string.Empty);
		createdResponse.Value.IsPermanent.ShouldBeFalse();
		createdResponse.Value.Tags.ShouldBeEmpty();

		var createdFlag = await fixture.Repository.GetAsync("default-flag");
		createdFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();
		createdFlag.UserAccessControl.HasAccessRestrictions().ShouldBeFalse();
		createdFlag.TargetingRules.ShouldBeEmpty();
	}
}