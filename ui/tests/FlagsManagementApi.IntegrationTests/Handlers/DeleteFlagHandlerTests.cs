using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Core;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover DeleteFlagHandler integration scenarios:
 * Successful deletion, non-existent flags, permanent flag protection, cache integration
 */

public class DeleteFlagHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagExists_ThenDeletesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("deletable-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.DeleteFlagHandler.HandleAsync("deletable-flag");

		// Assert
		var noContentResult = result.ShouldBeOfType<NoContent>();
		noContentResult.StatusCode.ShouldBe(204);

		// Verify flag was deleted from repository
		var deletedFlag = await fixture.Repository.GetAsync("deletable-flag");
		deletedFlag.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagExistsWithCache_ThenDeletesFromBothRepositoryAndCache()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-flag", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);
		
		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-flag", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-flag");
			cachedFlag.ShouldNotBeNull();
		}

		// Act
		var result = await fixture.DeleteFlagHandler.HandleAsync("cached-flag");

		// Assert
		result.ShouldBeOfType<NoContent>();

		// Verify flag was deleted from both repository and cache
		var deletedFlag = await fixture.Repository.GetAsync("cached-flag");
		deletedFlag.ShouldBeNull();

		if (fixture.Cache != null)
		{
			var cachedFlagAfterDelete = await fixture.Cache.GetAsync("cached-flag");
			cachedFlagAfterDelete.ShouldBeNull();
		}
	}
}

public class DeleteFlagHandler_NotFound(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();

		// Act
		var result = await fixture.DeleteFlagHandler.HandleAsync("non-existent-flag");

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(404);
		problemResponse.ProblemDetails.Detail.ShouldContain("non-existent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("was not found");
	}
}

public class DeleteFlagHandler_PermanentFlag(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagIsPermanent_ThenReturnsBadRequest()
	{
		// Arrange
		await fixture.ClearAllData();
		var permanentFlag = TestHelpers.CreateTestFlag("permanent-flag", FlagEvaluationMode.Enabled);
		permanentFlag.Lifecycle = new FlagLifecycle(expirationDate: null, isPermanent: true);
		await fixture.Repository.CreateAsync(permanentFlag);

		// Act
		var result = await fixture.DeleteFlagHandler.HandleAsync("permanent-flag");

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Title.ShouldBe("Cannot Delete Permanent Flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("permanent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("marked as permanent");

		// Verify flag was not deleted
		var existingFlag = await fixture.Repository.GetAsync("permanent-flag");
		existingFlag.ShouldNotBeNull();
	}
}

public class DeleteFlagHandler_ValidationErrors(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_KeyIsEmpty_ThenReturnsBadRequest()
	{
		// Act
		var result = await fixture.DeleteFlagHandler.HandleAsync("");

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}

	[Fact]
	public async Task If_KeyIsNull_ThenReturnsBadRequest()
	{
		// Act
		var result = await fixture.DeleteFlagHandler.HandleAsync(null!);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}
}