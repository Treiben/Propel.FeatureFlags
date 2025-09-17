using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

public class UpdateFlagHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidUpdateRequest_ThenUpdatesFlag()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("update-test-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateFlagRequest(
			Name: "Updated Flag Name",
			Description: "Updated description for testing",
			Tags: new Dictionary<string, string> { { "environment", "test" }, { "team", "platform" } },
			IsPermanent: true,
			ExpirationDate: null);

		// Act
		var result = await fixture.GetHandler<UpdateFlagHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var updateResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var ffResponse = updateResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Name.ShouldBe("Updated Flag Name");
		ffResponse.Description.ShouldBe("Updated description for testing");
		ffResponse.Tags.ShouldContainKeyAndValue("environment", "test");
		ffResponse.Tags.ShouldContainKeyAndValue("team", "platform");

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);
		
		updatedFlag.ShouldNotBeNull();

		// Check that properties updated correctly
		updatedFlag.Name.ShouldBe("Updated Flag Name");
		updatedFlag.Description.ShouldBe("Updated description for testing");
		updatedFlag.Tags.ShouldContainKeyAndValue("environment", "test");
		updatedFlag.Tags.ShouldContainKeyAndValue("team", "platform");
		updatedFlag.Retention.IsPermanent.ShouldBeTrue();

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_PartialUpdateRequest_ThenUpdatesOnlyProvidedFields()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("partial-update-flag", EvaluationMode.Disabled);
		flag.Name = "Original Name";
		flag.Description = "Original Description";
		flag.Tags = new Dictionary<string, string> { { "original", "value" } };
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateFlagRequest(
			Name: "Only Name Changed",
			Description: null, // Should not update description
			Tags: null, // Should not update tags
			IsPermanent: false,
			ExpirationDate: DateTimeOffset.UtcNow.AddDays(30));

		// Act
		var result = await fixture.GetHandler<UpdateFlagHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();
		ffResponse.Name.ShouldBe("Only Name Changed");

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that only specified fields were updated
		updatedFlag.Name.ShouldBe("Only Name Changed");
		updatedFlag.Description.ShouldBe("Original Description"); // Should remain unchanged
		updatedFlag.Tags.ShouldContainKeyAndValue("original", "value"); // Should remain unchanged
		updatedFlag.Retention.IsPermanent.ShouldBeFalse();
		updatedFlag.Retention.ExpirationDate.Date.ShouldBe(DateTimeOffset.UtcNow.AddDays(30).Date);

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_RetentionPolicyUpdate_ThenUpdatesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("retention-update-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var futureDate = DateTimeOffset.UtcNow.AddDays(90);
		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateFlagRequest(
			Name: null,
			Description: null,
			Tags: null,
			IsPermanent: false,
			ExpirationDate: futureDate);

		// Act
		var result = await fixture.GetHandler<UpdateFlagHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var ffResponse = okResult.Value;
		ffResponse.ShouldNotBeNull();

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that retention policy updated correctly
		updatedFlag.Retention.IsPermanent.ShouldBeFalse();
		updatedFlag.Retention.ExpirationDate.Date.ShouldBe(futureDate.Date);

		// Check that other properties remained unchanged
		updatedFlag.Name.ShouldBe(flag.Name);
		updatedFlag.Description.ShouldBe(flag.Description);
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();

		// Check that audit trail added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified!.Actor.ShouldBe("test-user");
	}
}
