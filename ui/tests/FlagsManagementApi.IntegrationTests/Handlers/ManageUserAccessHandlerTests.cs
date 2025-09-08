using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover ManageUserAccessHandler integration scenarios:
 * Enabling users, disabling users, multiple users, non-existent flags, validation errors
 */

public class ManageUserAccessHandler_EnableUsers(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_EnableSingleUser_ThenAddsToAllowedUsers()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("user-enable-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "user-123" };

		// Act
		var result = await fixture.ManageUserAccessHandler.HandleAsync("user-enable-flag", userIds, true, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.AllowedUsers.ShouldContain("user-123");
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("user-enable-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.UserAccess.AllowedUsers.ShouldContain("user-123");
		updatedFlag.AuditRecord.ModifiedBy.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_EnableMultipleUsers_ThenAddsAllToAllowedUsers()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("multi-user-enable", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "user-1", "user-2", "user-3" };

		// Act
		var result = await fixture.ManageUserAccessHandler.HandleAsync("multi-user-enable", userIds, true, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.AllowedUsers.ShouldContain("user-1");
		okResult.Value.AllowedUsers.ShouldContain("user-2");
		okResult.Value.AllowedUsers.ShouldContain("user-3");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("multi-user-enable");
		updatedFlag.UserAccess.AllowedUsers.ShouldContain("user-1");
		updatedFlag.UserAccess.AllowedUsers.ShouldContain("user-2");
		updatedFlag.UserAccess.AllowedUsers.ShouldContain("user-3");
	}
}

public class ManageUserAccessHandler_DisableUsers(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_DisableSingleUser_ThenAddsToBlockedUsers()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("user-disable-flag", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "blocked-user" };

		// Act
		var result = await fixture.ManageUserAccessHandler.HandleAsync("user-disable-flag", userIds, false, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.BlockedUsers.ShouldContain("blocked-user");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("user-disable-flag");
		updatedFlag.UserAccess.BlockedUsers.ShouldContain("blocked-user");
	}

	[Fact]
	public async Task If_DisableMultipleUsers_ThenAddsAllToBlockedUsers()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("multi-user-disable", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var userIds = new List<string> { "blocked-1", "blocked-2" };

		// Act
		var result = await fixture.ManageUserAccessHandler.HandleAsync("multi-user-disable", userIds, false, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.BlockedUsers.ShouldContain("blocked-1");
		okResult.Value.BlockedUsers.ShouldContain("blocked-2");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("multi-user-disable");
		updatedFlag.UserAccess.BlockedUsers.ShouldContain("blocked-1");
		updatedFlag.UserAccess.BlockedUsers.ShouldContain("blocked-2");
	}
}

public class ManageUserAccessHandler_NotFound(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();
		var userIds = new List<string> { "user-123" };

		// Act
		var result = await fixture.ManageUserAccessHandler.HandleAsync("non-existent-flag", userIds, true, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(404);
		problemResponse.ProblemDetails.Detail.ShouldContain("non-existent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("was not found");
	}
}

public class ManageUserAccessHandler_ValidationErrors(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_KeyIsEmpty_ThenReturnsBadRequest()
	{
		// Arrange
		var userIds = new List<string> { "user-123" };

		// Act
		var result = await fixture.ManageUserAccessHandler.HandleAsync("", userIds, true, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}

	[Fact]
	public async Task If_EmptyUserList_ThenReturnsBadRequest()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("empty-users-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var emptyUserIds = new List<string>();

		// Act
		var result = await fixture.ManageUserAccessHandler.HandleAsync("empty-users-flag", emptyUserIds, true, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("At least one user ID must be provided");
	}

	[Fact]
	public async Task If_NullUserList_ThenReturnsBadRequest()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("null-users-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.ManageUserAccessHandler.HandleAsync("null-users-flag", null!, true, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("At least one user ID must be provided");
	}
}

public class ManageUserAccessHandler_CacheIntegration(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagInCache_ThenRemovesFromCacheAfterUserUpdate()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-user-access", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-user-access", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-user-access");
			cachedFlag.ShouldNotBeNull();
		}

		var userIds = new List<string> { "cache-user" };

		// Act
		var result = await fixture.ManageUserAccessHandler.HandleAsync("cached-user-access", userIds, true, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify cache was cleared
		if (fixture.Cache != null)
		{
			var cachedFlagAfterUpdate = await fixture.Cache.GetAsync("cached-user-access");
			cachedFlagAfterUpdate.ShouldBeNull();
		}
	}
}