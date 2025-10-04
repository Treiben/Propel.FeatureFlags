using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace IntegrationTests.SqlServer.HandlersTests;

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
		flag.Administration.Name.ShouldBe("Test Flag");
		flag.Administration.Description.ShouldBe("Test description");
		flag.Administration.Tags["env"].ShouldBe("test");
	}

	[Fact]
	public async Task Should_return_conflict_when_flag_already_exists()
	{
		// Arrange
		var identifier = new FlagIdentifier("duplicate-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Existing",
						Description: "Already exists",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

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
		var identifier = new FlagIdentifier("deletable-flag", Scope.Application, applicationName: "test", applicationVersion: "1.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "To Delete",
						Description: "Will be deleted",
						RetentionPolicy: RetentionPolicy.OneMonthRetentionPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<DeleteFlagHandler>();
		var headers = new FlagRequestHeaders("Application", "test", "1.0.0.0");

		// Act
		var result = await handler.HandleAsync("deletable-flag", headers, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<NoContent>();

		var deleted = await fixture.DashboardRepository.GetByKeyAsync(identifier, CancellationToken.None);
		deleted.ShouldBeNull();
	}

	[Fact]
	public async Task Should_not_delete_permanent_flag()
	{
		// Arrange
		var identifier = new FlagIdentifier("permanent-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Permanent Flag",
						Description: "Cannot be deleted",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

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
		var identifier = new FlagIdentifier("cached-flag", Scope.Application, applicationName: "test", applicationVersion: "1.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Cached",
						Description: "In cache",
						RetentionPolicy: RetentionPolicy.OneMonthRetentionPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);


		var cacheKey = new ApplicationCacheKey(identifier.Key, identifier.ApplicationName, identifier.ApplicationVersion);
		await fixture.Cache.SetAsync(cacheKey, new EvaluationOptions(key: "cached-flag"));

		var handler = fixture.Services.GetRequiredService<DeleteFlagHandler>();
		var headers = new FlagRequestHeaders("Application", "test", "1.0.0.0");

		// Act
		await handler.HandleAsync("cached-flag", headers, CancellationToken.None);

		// Assert
		var cached = await fixture.Cache.GetAsync(cacheKey);
		cached.ShouldBeNull();
	}

	public Task InitializeAsync() => Task.CompletedTask;
	public Task DisposeAsync() => fixture.ClearAllData();
}