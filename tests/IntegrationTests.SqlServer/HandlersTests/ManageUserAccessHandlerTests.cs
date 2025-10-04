using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace IntegrationTests.SqlServer.HandlersTests;

public class ManageUserAccessHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_set_user_rollout_percentage_successfully()
	{
		// Arrange
		var identifier = new FlagIdentifier("user-percentage-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "User Percentage",
						Description: "Will have percentage",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<ManageUserAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageUserAccessRequest(null, null, 60, "Set 60% rollout");

		// Act
		var result = await handler.HandleAsync("user-percentage-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("user-percentage-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.UserAccessControl.RolloutPercentage.ShouldBe(60);
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.UserRolloutPercentage]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_add_allowed_users_successfully()
	{
		// Arrange
		var identifier = new FlagIdentifier("allowed-users-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Allowed Users",
						Description: "Will have allowed users",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<ManageUserAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageUserAccessRequest(
			new[] { "user-1", "user-2", "user-3" },
			null,
			null,
			"Adding allowed users");

		// Act
		var result = await handler.HandleAsync("allowed-users-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("allowed-users-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.UserAccessControl.Allowed.ShouldContain("user-1");
		updated.EvaluationOptions.UserAccessControl.Allowed.ShouldContain("user-2");
		updated.EvaluationOptions.UserAccessControl.Allowed.ShouldContain("user-3");
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.UserTargeted]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_add_blocked_users_successfully()
	{
		var identifier = new FlagIdentifier("blocked-users-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Blocked Users",
						Description: "Will have blocked users",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<ManageUserAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageUserAccessRequest(
			null,
			new[] { "user-blocked-1", "user-blocked-2" },
			null,
			"Blocking users");

		// Act
		var result = await handler.HandleAsync("blocked-users-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("blocked-users-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.UserAccessControl.Blocked.ShouldContain("user-blocked-1");
		updated.EvaluationOptions.UserAccessControl.Blocked.ShouldContain("user-blocked-2");
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.UserTargeted]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_remove_rollout_mode_when_percentage_is_one_hundred()
	{
		// Arrange
		var identifier = new FlagIdentifier("hundred-user-percentage-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Hundred User Percentage",
						Description: "Testing unrestricted user access",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<ManageUserAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageUserAccessRequest(null, null, 100, "Set to zero");

		// Act
		var result = await handler.HandleAsync("hundred-user-percentage-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("hundred-user-percentage-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.UserAccessControl.RolloutPercentage.ShouldBe(100);
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.UserRolloutPercentage]).ShouldBeFalse();
	}

	[Fact]
	public async Task Should_remove_on_off_modes_when_setting_user_access()
	{
		// Arrange
		var identifier = new FlagIdentifier("user-mode-cleanup-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "User Mode Cleanup",
						Description: "Remove on/off modese",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<ManageUserAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// First toggle it on
		var toggleHandler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		await toggleHandler.HandleAsync("user-mode-cleanup-flag", headers,
			new ToggleFlagRequest(EvaluationMode.On, "Enable first"), CancellationToken.None);

		// Act - Set user access
		var request = new ManageUserAccessRequest(null, null, 40, "Set user access");
		var result = await handler.HandleAsync("user-mode-cleanup-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("user-mode-cleanup-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.ModeSet.Contains([EvaluationMode.On]).ShouldBeFalse();
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.Off]).ShouldBeFalse();
	}

	[Fact]
	public async Task Should_set_both_allowed_and_percentage_together()
	{
		// Arrange
		var identifier = new FlagIdentifier("combined-user-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Combined User",
						Description: "Both allowed and percentage",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<ManageUserAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageUserAccessRequest(
			new[] { "user-alpha", "user-beta" },
			null,
			90,
			"Combined settings");

		// Act
		var result = await handler.HandleAsync("combined-user-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("combined-user-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.UserAccessControl.Allowed.ShouldContain("user-alpha");
		updated.EvaluationOptions.UserAccessControl.Allowed.ShouldContain("user-beta");
		updated.EvaluationOptions.UserAccessControl.RolloutPercentage.ShouldBe(90);
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.UserTargeted]).ShouldBeTrue();
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.UserRolloutPercentage]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_invalidate_cache_after_user_access_update()
	{
		// Arrange
		var identifier = new FlagIdentifier("cached-user-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Cached User",
						Description: "In cache",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var cacheKey = new GlobalCacheKey("cached-user-flag");
		await fixture.Cache.SetAsync(cacheKey, new EvaluationOptions(key: "cached-user-flag"));

		var handler = fixture.Services.GetRequiredService<ManageUserAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageUserAccessRequest(null, null, 25, "Update with cache clear");

		// Act
		await handler.HandleAsync("cached-user-flag", headers, request, CancellationToken.None);

		// Assert
		var cached = await fixture.Cache.GetAsync(cacheKey);
		cached.ShouldBeNull();
	}

	[Fact]
	public async Task Should_return_404_when_flag_not_found()
	{
		// Arrange
		var handler = fixture.Services.GetRequiredService<ManageUserAccessHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new ManageUserAccessRequest(null, null, 50, "Non-existent flag");

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
