using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace IntegrationTests.Postgres.HandlersTests;

public class ToggleFlagHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_toggle_flag_on_successfully()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("toggle-on-flag");
		await fixture.SaveAsync(flag, "Toggle On", "Will be enabled");

		var handler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ToggleFlagRequest(EvaluationMode.On, "Enabling for production");

		// Act
		var result = await handler.HandleAsync("toggle-on-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		var response = ((Ok<FeatureFlagResponse>)result).Value;
		response.ShouldNotBeNull();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("toggle-on-flag", Scope.Global), CancellationToken.None);
		updated!.EvalConfig.Modes.ContainsModes([EvaluationMode.On]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_toggle_flag_off_successfully()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("toggle-off-flag");
		await fixture.SaveAsync(flag, "Toggle Off", "Will be disabled");

		var handler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ToggleFlagRequest(EvaluationMode.Off, "Disabling temporarily");

		// Act
		var result = await handler.HandleAsync("toggle-off-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("toggle-off-flag", Scope.Global), CancellationToken.None);
		updated!.EvalConfig.Modes.ContainsModes([EvaluationMode.Off]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_return_ok_when_flag_already_in_requested_state()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("already-on-flag");
		await fixture.SaveAsync(flag, "Already On", "Already enabled");

		var handler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ToggleFlagRequest(EvaluationMode.On, "Trying to enable again");

		// Act - First toggle
		await handler.HandleAsync("already-on-flag", headers, request, CancellationToken.None);
		
		// Act - Second toggle with same state
		var result = await handler.HandleAsync("already-on-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
	}

	[Fact]
	public async Task Should_reset_access_control_when_toggling_on()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("access-control-flag");
		await fixture.SaveAsync(flag, "Access Control", "Will reset access");

		var handler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ToggleFlagRequest(EvaluationMode.On, "Enabling with full rollout");

		// Act
		var result = await handler.HandleAsync("access-control-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("access-control-flag", Scope.Global), CancellationToken.None);
		updated!.EvalConfig.UserAccessControl.RolloutPercentage.ShouldBe(100);
		updated.EvalConfig.TenantAccessControl.RolloutPercentage.ShouldBe(100);
	}

	[Fact]
	public async Task Should_reset_access_control_to_zero_when_toggling_off()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("access-off-flag");
		await fixture.SaveAsync(flag, "Access Off", "Will reset to zero");

		var handler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ToggleFlagRequest(EvaluationMode.Off, "Disabling completely");

		// Act
		var result = await handler.HandleAsync("access-off-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("access-off-flag", Scope.Global), CancellationToken.None);
		updated!.EvalConfig.UserAccessControl.RolloutPercentage.ShouldBe(0);
		updated.EvalConfig.TenantAccessControl.RolloutPercentage.ShouldBe(0);
	}

	[Fact]
	public async Task Should_invalidate_cache_after_toggle()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("cached-toggle-flag");
		await fixture.SaveAsync(flag, "Cached Toggle", "In cache");

		var cacheKey = new GlobalCacheKey("cached-toggle-flag");
		await fixture.Cache.SetAsync(cacheKey, flag);

		var handler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ToggleFlagRequest(EvaluationMode.On, "Toggle and clear cache");

		// Act
		await handler.HandleAsync("cached-toggle-flag", headers, request, CancellationToken.None);

		// Assert
		var cached = await fixture.Cache.GetAsync(cacheKey);
		cached.ShouldBeNull();
	}

	[Fact]
	public async Task Should_return_404_when_flag_not_found()
	{
		// Arrange
		var handler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ToggleFlagRequest(EvaluationMode.On, "Toggle non-existent");

		// Act
		var result = await handler.HandleAsync("non-existent", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<ProblemHttpResult>();
		var problem = (ProblemHttpResult)result;
		problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
	}

	public Task InitializeAsync() => Task.CompletedTask;
	public Task DisposeAsync() => fixture.ClearAllData();
}
