using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover UpdateFlagHandler integration scenarios:
 * Successful updates, non-existent flags, partial updates, user access control updates
 */

public class UpdateFlagHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidRequest_ThenUpdatesFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("update-test-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateFlagRequest
		{
			Name = "Updated Flag Name",
			Description = "Updated description",
			Tags = new Dictionary<string, string> { { "team", "updated-team" } },
			IsPermanent = true
		};

		// Act
		var result = await fixture.UpdateFlagHandler.HandleAsync("update-test-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.Name.ShouldBe("Updated Flag Name");
		okResult.Value.Description.ShouldBe("Updated description");
		okResult.Value.Tags.ShouldContainKeyAndValue("team", "updated-team");
		okResult.Value.IsPermanent.ShouldBeTrue();
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("update-test-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.Name.ShouldBe("Updated Flag Name");
		updatedFlag.Description.ShouldBe("Updated description");
		updatedFlag.Lifecycle.IsPermanent.ShouldBeTrue();
		updatedFlag.AuditRecord.ModifiedBy.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_PartialUpdate_ThenUpdatesOnlyProvidedFields()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("partial-update-flag", FlagEvaluationMode.Enabled);
		flag.Name = "Original Name";
		flag.Description = "Original Description";
		flag.Tags = new Dictionary<string, string> { { "env", "prod" } };
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateFlagRequest
		{
			Name = "Updated Name Only"
			// Description and Tags are null - should not be updated
		};

		// Act
		var result = await fixture.UpdateFlagHandler.HandleAsync("partial-update-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.Name.ShouldBe("Updated Name Only");
		okResult.Value.Description.ShouldBe("Original Description");
		okResult.Value.Tags.ShouldContainKeyAndValue("env", "prod");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("partial-update-flag");
		updatedFlag.Name.ShouldBe("Updated Name Only");
		updatedFlag.Description.ShouldBe("Original Description");
	}
}

public class UpdateFlagHandler_UserAccessControl(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_UserAccessUpdated_ThenSetsCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("user-access-flag", FlagEvaluationMode.UserTargeted);
		flag.UserAccess = new FlagUserAccessControl(rolloutPercentage: 50);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateFlagRequest
		{
			AllowedUsers = ["user1", "user2"],
			BlockedUsers = ["blocked-user"]
		};

		// Act
		var result = await fixture.UpdateFlagHandler.HandleAsync("user-access-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.AllowedUsers.ShouldContain("user1");
		okResult.Value.AllowedUsers.ShouldContain("user2");
		okResult.Value.BlockedUsers.ShouldContain("blocked-user");
		okResult.Value.UserRolloutPercentage.ShouldBe(50); // Should preserve existing rollout percentage

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("user-access-flag");
		updatedFlag.UserAccess.AllowedUsers.ShouldContain("user1");
		updatedFlag.UserAccess.BlockedUsers.ShouldContain("blocked-user");
		updatedFlag.UserAccess.RolloutPercentage.ShouldBe(50);
	}

	[Fact]
	public async Task If_EmptyUserLists_ThenClearsUserAccess()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("clear-users-flag", FlagEvaluationMode.UserTargeted);
		flag.UserAccess = new FlagUserAccessControl(["existing-user"], ["blocked-user"], 75);
		await fixture.Repository.CreateAsync(flag);

		var request = new UpdateFlagRequest
		{
			AllowedUsers = [],
			BlockedUsers = []
		};

		// Act
		var result = await fixture.UpdateFlagHandler.HandleAsync("clear-users-flag", request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.AllowedUsers.ShouldBeEmpty();
		okResult.Value.BlockedUsers.ShouldBeEmpty();
		okResult.Value.UserRolloutPercentage.ShouldBe(75); // Should preserve rollout percentage

		var updatedFlag = await fixture.Repository.GetAsync("clear-users-flag");
		updatedFlag.UserAccess.AllowedUsers.ShouldBeEmpty();
		updatedFlag.UserAccess.BlockedUsers.ShouldBeEmpty();
	}
}

public class UpdateFlagHandler_NotFound(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();
		var request = new UpdateFlagRequest { Name = "Updated Name" };

		// Act
		var result = await fixture.UpdateFlagHandler.HandleAsync("non-existent-flag", request, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(404);
		problemResponse.ProblemDetails.Detail.ShouldContain("non-existent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("was not found");
	}
}

public class UpdateFlagHandler_ValidationErrors(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_KeyIsEmpty_ThenReturnsBadRequest()
	{
		// Arrange
		var request = new UpdateFlagRequest { Name = "Test" };

		// Act
		var result = await fixture.UpdateFlagHandler.HandleAsync("", request, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}
}

public class UpdateFlagHandler_CacheIntegration(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagInCache_ThenRemovesFromCacheAfterUpdate()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-update-flag", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-update-flag", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-update-flag");
			cachedFlag.ShouldNotBeNull();
		}

		var request = new UpdateFlagRequest { Name = "Updated Cached Flag" };

		// Act
		var result = await fixture.UpdateFlagHandler.HandleAsync("cached-update-flag", request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify cache was cleared
		if (fixture.Cache != null)
		{
			var cachedFlagAfterUpdate = await fixture.Cache.GetAsync("cached-update-flag");
			cachedFlagAfterUpdate.ShouldBeNull();
		}
	}
}
