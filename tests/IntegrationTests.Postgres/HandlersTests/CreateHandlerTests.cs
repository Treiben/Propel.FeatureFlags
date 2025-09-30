using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace IntegrationTests.Postgres.HandlersTests;

public class CreateFlagHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_create_global_flag_successfully()
	{
		// Arrange
		var request = new CreateGlobalFeatureFlagRequest
		{
			Key = "test-flag",
			Name = "Test Flag",
			Description = "Test description",
			Tags = new Dictionary<string, string> { ["env"] = "test" }
		};
		var handler = fixture.Services.GetRequiredService<CreateGlobalFlagHandler>();

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Created<FeatureFlagResponse>>();

		var flag = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("test-flag", Scope.Global), CancellationToken.None);

		flag.ShouldNotBeNull();
		flag.Metadata.Name.ShouldBe("Test Flag");
		flag.Metadata.Description.ShouldBe("Test description");
		flag.Metadata.Tags["env"].ShouldBe("test");
	}

	[Fact]
	public async Task Should_return_conflict_when_flag_already_exists()
	{
		// Arrange
		var existingFlag = FlagEvaluationConfiguration.CreateGlobal("duplicate-flag");
		await fixture.SaveAsync(existingFlag, "Existing", "Already exists");

		var request = new CreateGlobalFeatureFlagRequest
		{
			Key = "duplicate-flag",
			Name = "Duplicate"
		};
		var handler = fixture.Services.GetRequiredService<CreateGlobalFlagHandler>();

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<ProblemHttpResult>();
		var problem = (ProblemHttpResult)result;
		problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
	}

	public Task InitializeAsync() => Task.CompletedTask;
	public Task DisposeAsync() => fixture.ClearAllData();
}

public class DeleteFlagHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_delete_flag_successfully()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("deletable-flag");
		await fixture.SaveAsync(flag, "To Delete", "Will be deleted");

		var handler = fixture.Services.GetRequiredService<DeleteFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// Act
		var result = await handler.HandleAsync("deletable-flag", headers, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<NoContent>();

		var deleted = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("deletable-flag", Scope.Global), CancellationToken.None);
		deleted.ShouldBeNull();
	}

	[Fact]
	public async Task Should_not_delete_permanent_flag()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("permanent-flag");

		await fixture.SaveAsync(flag, "Permanent", "Cannot delete");

		// Mark as permanent
		var stored = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("permanent-flag", Scope.Global), CancellationToken.None);

		await fixture.DashboardRepository.UpdateAsync(stored, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<DeleteFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// Act
		var result = await handler.HandleAsync("permanent-flag", headers, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<ProblemHttpResult>();
		var problem = (ProblemHttpResult)result;
		problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
	}

	[Fact]
	public async Task Should_return_404_when_flag_not_found()
	{
		// Arrange
		var handler = fixture.Services.GetRequiredService<DeleteFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// Act
		var result = await handler.HandleAsync("non-existent", headers, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<ProblemHttpResult>();
		var problem = (ProblemHttpResult)result;
		problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
	}

	[Fact]
	public async Task Should_invalidate_cache_after_deletion()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("cached-flag");
		await fixture.SaveAsync(flag, "Cached", "In cache");

		var identifier = new FlagIdentifier("cached-flag", Scope.Global);
		var cacheKey = new GlobalCacheKey(flag.Identifier.Key);
		await fixture.Cache.SetAsync(cacheKey, flag);

		var handler = fixture.Services.GetRequiredService<DeleteFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// Act
		await handler.HandleAsync("cached-flag", headers, CancellationToken.None);

		// Assert
		var cached = await fixture.Cache.GetAsync(cacheKey);
		cached.ShouldBeNull();
	}

	public Task InitializeAsync() => Task.CompletedTask;
	public Task DisposeAsync() => fixture.ClearAllData();
}