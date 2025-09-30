using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace IntegrationTests.Postgres.HandlersTests;

public class ManageTenantAccessHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_set_tenant_rollout_percentage_successfully()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("tenant-percentage-flag");
		await fixture.SaveAsync(flag, "Tenant Percentage", "Will have percentage");

		var handler = fixture.Services.GetRequiredService<ManageTenantAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageTenantAccessRequest(null, null, 75, "Set 75% rollout");

		// Act
		var result = await handler.HandleAsync("tenant-percentage-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("tenant-percentage-flag", Scope.Global), CancellationToken.None);
		updated!.EvalConfig.TenantAccessControl.RolloutPercentage.ShouldBe(75);
		updated.EvalConfig.Modes.ContainsModes([EvaluationMode.TenantRolloutPercentage]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_add_allowed_tenants_successfully()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("allowed-tenants-flag");
		await fixture.SaveAsync(flag, "Allowed Tenants", "Will have allowed tenants");

		var handler = fixture.Services.GetRequiredService<ManageTenantAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageTenantAccessRequest(
			new[] { "tenant-1", "tenant-2" }, 
			null, 
			null, 
			"Adding allowed tenants");

		// Act
		var result = await handler.HandleAsync("allowed-tenants-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("allowed-tenants-flag", Scope.Global), CancellationToken.None);
		updated!.EvalConfig.TenantAccessControl.Allowed.ShouldContain("tenant-1");
		updated.EvalConfig.TenantAccessControl.Allowed.ShouldContain("tenant-2");
		updated.EvalConfig.Modes.ContainsModes([EvaluationMode.TenantTargeted]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_add_blocked_tenants_successfully()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("blocked-tenants-flag");
		await fixture.SaveAsync(flag, "Blocked Tenants", "Will have blocked tenants");

		var handler = fixture.Services.GetRequiredService<ManageTenantAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageTenantAccessRequest(
			null, 
			new[] { "tenant-bad-1", "tenant-bad-2" }, 
			null, 
			"Blocking tenants");

		// Act
		var result = await handler.HandleAsync("blocked-tenants-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("blocked-tenants-flag", Scope.Global), CancellationToken.None);
		updated!.EvalConfig.TenantAccessControl.Blocked.ShouldContain("tenant-bad-1");
		updated.EvalConfig.TenantAccessControl.Blocked.ShouldContain("tenant-bad-2");
		updated.EvalConfig.Modes.ContainsModes([EvaluationMode.TenantTargeted]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_remove_rollout_mode_when_percentage_is_zero()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("zero-percentage-flag");
		await fixture.SaveAsync(flag, "Zero Percentage", "Test zero percentage");

		var handler = fixture.Services.GetRequiredService<ManageTenantAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageTenantAccessRequest(null, null, 0, "Set to zero");

		// Act
		var result = await handler.HandleAsync("zero-percentage-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("zero-percentage-flag", Scope.Global), CancellationToken.None);
		updated!.EvalConfig.TenantAccessControl.RolloutPercentage.ShouldBe(0);
		updated.EvalConfig.Modes.ContainsModes([EvaluationMode.TenantRolloutPercentage]).ShouldBeFalse();
	}

	[Fact]
	public async Task Should_remove_on_off_modes_when_setting_tenant_access()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("tenant-mode-cleanup-flag");
		await fixture.SaveAsync(flag, "Tenant Mode Cleanup", "Remove on/off modes");

		var handler = fixture.Services.GetRequiredService<ManageTenantAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		
		// First toggle it on
		var toggleHandler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		await toggleHandler.HandleAsync("tenant-mode-cleanup-flag", headers, 
			new ToggleFlagRequest(EvaluationMode.On, "Enable first"), CancellationToken.None);

		// Act - Set tenant access
		var request = new ManageTenantAccessRequest(null, null, 50, "Set tenant access");
		var result = await handler.HandleAsync("tenant-mode-cleanup-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("tenant-mode-cleanup-flag", Scope.Global), CancellationToken.None);
		updated!.EvalConfig.Modes.ContainsModes([EvaluationMode.On]).ShouldBeFalse();
		updated.EvalConfig.Modes.ContainsModes([EvaluationMode.Off]).ShouldBeFalse();
	}

	[Fact]
	public async Task Should_set_both_allowed_and_percentage_together()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("combined-tenant-flag");
		await fixture.SaveAsync(flag, "Combined Tenant", "Both allowed and percentage");

		var handler = fixture.Services.GetRequiredService<ManageTenantAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageTenantAccessRequest(
			new[] { "tenant-a" }, 
			null, 
			80, 
			"Combined settings");

		// Act
		var result = await handler.HandleAsync("combined-tenant-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		
		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("combined-tenant-flag", Scope.Global), CancellationToken.None);
		updated!.EvalConfig.TenantAccessControl.Allowed.ShouldContain("tenant-a");
		updated.EvalConfig.TenantAccessControl.RolloutPercentage.ShouldBe(80);
		updated.EvalConfig.Modes.ContainsModes([EvaluationMode.TenantTargeted]).ShouldBeTrue();
		updated.EvalConfig.Modes.ContainsModes([EvaluationMode.TenantRolloutPercentage]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_invalidate_cache_after_tenant_access_update()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("cached-tenant-flag");
		await fixture.SaveAsync(flag, "Cached Tenant", "In cache");

		var cacheKey = new GlobalCacheKey("cached-tenant-flag");
		await fixture.Cache.SetAsync(cacheKey, flag);

		var handler = fixture.Services.GetRequiredService<ManageTenantAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageTenantAccessRequest(null, null, 60, "Update with cache clear");

		// Act
		await handler.HandleAsync("cached-tenant-flag", headers, request, CancellationToken.None);

		// Assert
		var cached = await fixture.Cache.GetAsync(cacheKey);
		cached.ShouldBeNull();
	}

	[Fact]
	public async Task Should_return_404_when_flag_not_found()
	{
		// Arrange
		var handler = fixture.Services.GetRequiredService<ManageTenantAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageTenantAccessRequest(null, null, 50, "Non-existent flag");

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
