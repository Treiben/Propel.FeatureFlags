using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace IntegrationTests.SqlServer.HandlersTests;

public class UpdateScheduleHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_set_schedule_successfully()
	{
		// Arrange
		var identifier = new FlagIdentifier("update-schedule-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Schedule Flag",
						Description: "Will be scheduled",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<UpdateScheduleHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var enableOn = DateTimeOffset.UtcNow.AddDays(1);
		var disableOn = DateTimeOffset.UtcNow.AddDays(7);
		var request = new UpdateScheduleRequest(enableOn, disableOn, "Setting weekly schedule");

		// Act
		var result = await handler.HandleAsync("update-schedule-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("update-schedule-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.ModeSet.Contains([EvaluationMode.Scheduled]).ShouldBeTrue();
		updated.EvaluationOptions.Schedule.EnableOn.DateTime.ShouldNotBe(DateTime.MinValue.ToUniversalTime());
		updated.EvaluationOptions.Schedule.DisableOn.DateTime.ShouldNotBe(DateTime.MaxValue.ToUniversalTime());
	}

	[Fact]
	public async Task Should_remove_schedule_successfully()
	{
		// Arrange
		var identifier = new FlagIdentifier("remove-schedule-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Remove Schedule",
						Description: "Has schedule to remove",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<UpdateScheduleHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// First set a schedule
		var enableOn = DateTimeOffset.UtcNow.AddDays(1);
		var disableOn = DateTimeOffset.UtcNow.AddDays(7);
		await handler.HandleAsync("remove-schedule-flag", headers,
			new UpdateScheduleRequest(enableOn, disableOn, "Adding schedule"), CancellationToken.None);

		// Act - Remove schedule
		var request = new UpdateScheduleRequest(null, null, "Removing schedule");
		var result = await handler.HandleAsync("remove-schedule-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("remove-schedule-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.ModeSet.Contains([EvaluationMode.Scheduled]).ShouldBeFalse();
	}

	[Fact]
	public async Task Should_add_scheduled_mode_when_setting_schedule()
	{
		// Arrange
		var identifier = new FlagIdentifier("mode-schedule-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Mode Schedule",
						Description: "Check mode addition",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<UpdateScheduleHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var enableOn = DateTimeOffset.UtcNow.AddDays(1);
		var disableOn = DateTimeOffset.UtcNow.AddDays(2);
		var request = new UpdateScheduleRequest(enableOn, disableOn, "Adding scheduled mode");

		// Act
		var result = await handler.HandleAsync("mode-schedule-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("mode-schedule-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.ModeSet.Contains([EvaluationMode.Scheduled]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_remove_on_off_modes_when_scheduling()
	{
		// Arrange
		var identifier = new FlagIdentifier("mode-cleanup-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Mode Cleanup",
						Description: "Remove on/off modes",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<UpdateScheduleHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// First toggle it on
		var toggleHandler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		await toggleHandler.HandleAsync("mode-cleanup-flag", headers,
			new ToggleFlagRequest(EvaluationMode.On, "Enable first"), CancellationToken.None);

		// Act - Set schedule
		var enableOn = DateTimeOffset.UtcNow.AddDays(1);
		var disableOn = DateTimeOffset.UtcNow.AddDays(3);
		var request = new UpdateScheduleRequest(enableOn, disableOn, "Schedule it");
		var result = await handler.HandleAsync("mode-cleanup-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("mode-cleanup-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.ModeSet.Contains([EvaluationMode.On]).ShouldBeFalse();
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.Off]).ShouldBeFalse();
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.Scheduled]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_invalidate_cache_after_schedule_update()
	{
		// Arrange
		var identifier = new FlagIdentifier("cached-schedule-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Cached Schedule",
						Description: "In cache",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var cacheKey = new GlobalCacheKey("cached-schedule-flag");
		await fixture.Cache.SetAsync(cacheKey, new EvaluationOptions(key: "cached-schedule-flag"));

		var handler = fixture.Services.GetRequiredService<UpdateScheduleHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var enableOn = DateTimeOffset.UtcNow.AddDays(1);
		var disableOn = DateTimeOffset.UtcNow.AddDays(5);
		var request = new UpdateScheduleRequest(enableOn, disableOn, "Schedule with cache clear");

		// Act
		await handler.HandleAsync("cached-schedule-flag", headers, request, CancellationToken.None);

		// Assert
		var cached = await fixture.Cache.GetAsync(cacheKey);
		cached.ShouldBeNull();
	}

	[Fact]
	public async Task Should_return_404_when_flag_not_found()
	{
		// Arrange
		var handler = fixture.Services.GetRequiredService<UpdateScheduleHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var enableOn = DateTimeOffset.UtcNow.AddDays(1);
		var disableOn = DateTimeOffset.UtcNow.AddDays(2);
		var request = new UpdateScheduleRequest(enableOn, disableOn, "Schedule non-existent");

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
