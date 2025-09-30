using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace IntegrationTests.Postgres.HandlersTests;

public class UpdateFlagHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_update_flag_name_successfully()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("update-name-flag");
		await fixture.SaveAsync(flag, "Old Name", "Description");

		var handler = fixture.Services.GetRequiredService<UpdateFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new UpdateFlagRequest("New Name", null, null, null, null);

		// Act
		var result = await handler.HandleAsync("update-name-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var response = ((Ok<FeatureFlagResponse>)result).Value;
		response.Name.ShouldBe("New Name");

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("update-name-flag", Scope.Global), CancellationToken.None);
		updated!.Metadata.Name.ShouldBe("New Name");
	}

	[Fact]
	public async Task Should_update_flag_description_successfully()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("update-desc-flag");
		await fixture.SaveAsync(flag, "Name", "Old Description");

		var handler = fixture.Services.GetRequiredService<UpdateFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new UpdateFlagRequest(null, "New Description", null, null, null);

		// Act
		var result = await handler.HandleAsync("update-desc-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var response = ((Ok<FeatureFlagResponse>)result).Value;
		response.Description.ShouldBe("New Description");

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("update-desc-flag", Scope.Global), CancellationToken.None);
		updated!.Metadata.Description.ShouldBe("New Description");
	}

	[Fact]
	public async Task Should_update_flag_tags_successfully()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("update-tags-flag");
		await fixture.SaveAsync(flag, "Name", "Description");

		var handler = fixture.Services.GetRequiredService<UpdateFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var newTags = new Dictionary<string, string> { ["env"] = "production", ["team"] = "backend" };
		var request = new UpdateFlagRequest(null, null, newTags, null, null);

		// Act
		var result = await handler.HandleAsync("update-tags-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("update-tags-flag", Scope.Global), CancellationToken.None);
		updated!.Metadata.Tags["env"].ShouldBe("production");
		updated.Metadata.Tags["team"].ShouldBe("backend");
	}

	[Fact]
	public async Task Should_update_flag_expiration_date_successfully()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("update-expiration-flag");
		await fixture.SaveAsync(flag, "Name", "Description");

		var handler = fixture.Services.GetRequiredService<UpdateFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var expirationDate = DateTimeOffset.UtcNow.AddDays(30);
		var request = new UpdateFlagRequest(null, null, null, expirationDate, null);

		// Act
		var result = await handler.HandleAsync("update-expiration-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("update-expiration-flag", Scope.Global), CancellationToken.None);
		updated!.Metadata.RetentionPolicy.ShouldNotBeNull();
	}

	[Fact]
	public async Task Should_update_multiple_fields_at_once()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("update-multiple-flag");
		await fixture.SaveAsync(flag, "Old Name", "Old Description");

		var handler = fixture.Services.GetRequiredService<UpdateFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var newTags = new Dictionary<string, string> { ["updated"] = "true" };
		var request = new UpdateFlagRequest("New Name", "New Description", newTags, null, "Bulk update");

		// Act
		var result = await handler.HandleAsync("update-multiple-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("update-multiple-flag", Scope.Global), CancellationToken.None);
		updated!.Metadata.Name.ShouldBe("New Name");
		updated.Metadata.Description.ShouldBe("New Description");
		updated.Metadata.Tags["updated"].ShouldBe("true");
	}

	[Fact]
	public async Task Should_invalidate_cache_after_update()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("cached-update-flag");
		await fixture.SaveAsync(flag, "Cached", "In cache");

		var cacheKey = new GlobalCacheKey("cached-update-flag");
		await fixture.Cache.SetAsync(cacheKey, flag);

		var handler = fixture.Services.GetRequiredService<UpdateFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new UpdateFlagRequest("Updated Name", null, null, null, null);

		// Act
		await handler.HandleAsync("cached-update-flag", headers, request, CancellationToken.None);

		// Assert
		var cached = await fixture.Cache.GetAsync(cacheKey);
		cached.ShouldBeNull();
	}

	[Fact]
	public async Task Should_return_404_when_flag_not_found()
	{
		// Arrange
		var handler = fixture.Services.GetRequiredService<UpdateFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new UpdateFlagRequest("New Name", null, null, null, null);

		// Act
		var result = await handler.HandleAsync("non-existent", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<ProblemHttpResult>();
		var problem = (ProblemHttpResult)result;
		problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
	}

	[Fact]
	public async Task Should_preserve_existing_values_when_fields_are_null()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("preserve-flag");
		await fixture.SaveAsync(flag, "Original Name", "Original Description");

		var handler = fixture.Services.GetRequiredService<UpdateFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new UpdateFlagRequest(null, "Only description updated", null, null, null);

		// Act
		var result = await handler.HandleAsync("preserve-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("preserve-flag", Scope.Global), CancellationToken.None);
		updated!.Metadata.Name.ShouldBe("Original Name");
		updated.Metadata.Description.ShouldBe("Only description updated");
	}

	public Task InitializeAsync() => Task.CompletedTask;
	public Task DisposeAsync() => fixture.ClearAllData();
}
