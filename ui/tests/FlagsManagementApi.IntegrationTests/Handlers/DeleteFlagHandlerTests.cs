using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

public class DeleteFlagHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagExists_ThenDeletesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("deletable-flag", EvaluationMode.Disabled);

		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		// Act
		var result = await fixture.GetHandler<DeleteFlagHandler>().HandleAsync(flag.Key.Key, headers, reason: null, cancellationToken: CancellationToken.None);

		// Assert
		var noContentResult = result.ShouldBeOfType<NoContent>();
		noContentResult.StatusCode.ShouldBe(204);

		// Verify flag was deleted from repository
		var deletedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);
		deletedFlag.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagExistsWithCache_ThenDeletesFromBothRepositoryAndCache()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateApplicationFlag("cached-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var cacheKey = new ApplicationCacheKey(flag.Key.Key, flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync(
				cacheKey, 
				new EvaluationCriteria
				{
					FlagKey = flag.Key.Key,
					ActiveEvaluationModes = flag.ActiveEvaluationModes,
				});
			var cachedFlag = await fixture.Cache.GetAsync(cacheKey);
			cachedFlag.ShouldNotBeNull();
		}

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		// Act
		var result = await fixture.GetHandler<DeleteFlagHandler>().HandleAsync(flag.Key.Key, headers, reason: null, cancellationToken: CancellationToken.None);

		// Assert
		result.ShouldBeOfType<NoContent>();

		// Verify flag was deleted from both repository and cache
		var deletedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);
		deletedFlag.ShouldBeNull();

		if (fixture.Cache != null)
		{
			var cachedFlagAfterDelete = await fixture.Cache.GetAsync(cacheKey);
			cachedFlagAfterDelete.ShouldBeNull();
		}
	}
}

public class DeleteFlagHandler_PermanentFlag(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagIsPermanent_ThenReturnsBadRequest()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateGlobalFlag("permanent-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Global.ToString(), null, null);
		// Act
		var result = await fixture.GetHandler<DeleteFlagHandler>().HandleAsync(flag.Key.Key,
			headers,
			reason: null, 
			cancellationToken: CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Title.ShouldBe("Cannot Delete Permanent Flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("permanent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("marked as permanent");

		// Verify flag was not deleted
		var existingFlag = await fixture.ManagementRepository.GetAsync(flag.Key);
		existingFlag.ShouldNotBeNull();
	}
}