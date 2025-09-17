using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

public class CreateFlagHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidRequest_ThenCreatesFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var request = new CreateGlobalFeatureFlagRequest
		{
			Key = "test-flag-1",
			Name = "Test Flag 1",
			Description = "A test flag",
			Tags = new Dictionary<string, string> { { "env", "dev" } }
		};

		// Act
		var result = await fixture.GetHandler<CreateGlobalFlagHandler>().HandleAsync(request, CancellationToken.None);

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
		var createdFlag = await fixture.ManagementRepository.GetAsync(new FlagKey(request.Key, Scope.Global));

		createdFlag.ShouldNotBeNull();
		createdFlag.Key.Key.ShouldBe("test-flag-1");
		createdFlag.Name.ShouldBe("Test Flag 1");
		createdFlag.Created.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_RequestWithTags_ThenCreatesWithTags()
	{
		// Arrange
		await fixture.ClearAllData();
		var request = new CreateGlobalFeatureFlagRequest
		{
			Key = "tagged-flag",
			Name = "Tagged Flag",
			Tags = new Dictionary<string, string> { { "team", "platform" }, { "env", "test" } }
		};

		// Act
		var result = await fixture.GetHandler<CreateGlobalFlagHandler>().HandleAsync(request, CancellationToken.None);

		// Assert
		var createdResponse = result.ShouldBeOfType<Created<FeatureFlagResponse>>();
		createdResponse.Value.Tags.ShouldContainKeyAndValue("team", "platform");
		createdResponse.Value.Tags.ShouldContainKeyAndValue("env", "test");

		var createdFlag = await fixture.ManagementRepository.GetAsync(new FlagKey(request.Key, Scope.Global));
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
		var existingFlag = TestHelpers.CreateGlobalFlag("duplicate-key", EvaluationMode.Disabled);
		await fixture.ManagementRepository.CreateAsync(existingFlag);

		var request = new CreateGlobalFeatureFlagRequest
		{
			Key = "duplicate-key",
			Name = "Duplicate Flag"
		};

		// Act
		var result = await fixture.GetHandler<CreateGlobalFlagHandler>().HandleAsync(request, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(409);
		problemResponse.ProblemDetails.Detail.ShouldContain("duplicate-key");
		problemResponse.ProblemDetails.Detail.ShouldContain("already exists");
	}
}
