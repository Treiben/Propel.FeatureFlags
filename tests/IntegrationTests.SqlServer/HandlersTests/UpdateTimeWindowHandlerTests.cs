using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace IntegrationTests.SqlServer.HandlersTests;

public class UpdateTimeWindowHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_set_time_window_successfully()
	{
		// Arrange
		var identifier = new FlagIdentifier("time-window-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Time Window",
						Description: "Will have time window",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);


		var handler = fixture.Services.GetRequiredService<UpdateTimeWindowHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new UpdateTimeWindowRequest(
			new TimeOnly(9, 0),
			new TimeOnly(17, 0),
			"America/New_York",
			new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
			false,
			"Business hours only");

		// Act
		var result = await handler.HandleAsync("time-window-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("time-window-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.ModeSet.Contains([EvaluationMode.TimeWindow]).ShouldBeTrue();
		updated.EvaluationOptions.OperationalWindow.DaysActive.Count().ShouldBe(3);
	}

	[Fact]
	public async Task Should_remove_time_window_successfully()
	{
		// Arrange
		var identifier = new FlagIdentifier("remove-window-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Remove Window",
						Description: "Has window to remove",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<UpdateTimeWindowHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// First set a window
		await handler.HandleAsync("remove-window-flag", headers,
			new UpdateTimeWindowRequest(
				new TimeOnly(9, 0),
				new TimeOnly(17, 0),
				"UTC",
				new List<DayOfWeek> { DayOfWeek.Monday },
				false,
				"Add window"),
			CancellationToken.None);

		// Act - Remove window
		var request = new UpdateTimeWindowRequest(
			TimeOnly.MinValue,
			TimeOnly.MinValue,
			string.Empty,
			new List<DayOfWeek>(),
			true,
			"Remove window");
		var result = await handler.HandleAsync("remove-window-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("remove-window-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.ModeSet.Contains([EvaluationMode.TimeWindow]).ShouldBeFalse();
	}

	[Fact]
	public async Task Should_add_time_window_mode_when_setting_window()
	{
		// Arrange
		var identifier = new FlagIdentifier("mode-window-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Mode Window",
						Description: "Check mode addition",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<UpdateTimeWindowHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new UpdateTimeWindowRequest(
			new TimeOnly(8, 0),
			new TimeOnly(20, 0),
			"Europe/London",
			new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday },
			false,
			"Set window");

		// Act
		var result = await handler.HandleAsync("mode-window-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("mode-window-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.ModeSet.Contains([EvaluationMode.TimeWindow]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_remove_on_off_modes_when_setting_time_window()
	{
		// Arrange
		var identifier = new FlagIdentifier("cleanup-window-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Cleanup Window",
						Description: "Remove on/off modes",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<UpdateTimeWindowHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// First toggle it on
		var toggleHandler = fixture.Services.GetRequiredService<ToggleFlagHandler>();
		await toggleHandler.HandleAsync("cleanup-window-flag", headers,
			new ToggleFlagRequest(EvaluationMode.On, "Enable first"), CancellationToken.None);

		// Act - Set time window
		var request = new UpdateTimeWindowRequest(
			new TimeOnly(10, 0),
			new TimeOnly(18, 0),
			"UTC",
			new List<DayOfWeek> { DayOfWeek.Thursday },
			false,
			"Set window");
		var result = await handler.HandleAsync("cleanup-window-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("cleanup-window-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.ModeSet.Contains([EvaluationMode.On]).ShouldBeFalse();
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.Off]).ShouldBeFalse();
		updated.EvaluationOptions.ModeSet.Contains([EvaluationMode.TimeWindow]).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_set_time_window_with_all_days_active()
	{
		// Arrange
		var identifier = new FlagIdentifier("all-days-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "All Days",
						Description: "All days active",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<UpdateTimeWindowHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new UpdateTimeWindowRequest(
			new TimeOnly(0, 0),
			new TimeOnly(23, 59),
			"UTC",
			new List<DayOfWeek>
			{
				DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
				DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
			},
			false,
			"24/7 window");

		// Act
		var result = await handler.HandleAsync("all-days-flag", headers, request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		var updated = await fixture.DashboardRepository.GetByKeyAsync(
			new FlagIdentifier("all-days-flag", Scope.Global), CancellationToken.None);
		updated!.EvaluationOptions.OperationalWindow.DaysActive.Count().ShouldBe(7);
	}

	[Fact]
	public async Task Should_invalidate_cache_after_time_window_update()
	{
		// Arrange
		var identifier = new FlagIdentifier("cached-window-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Cached Window",
						Description: "In cache",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var cacheKey = new GlobalCacheKey("cached-window-flag");
		await fixture.Cache.SetAsync(cacheKey, new EvaluationOptions(key: "cached-window-flag"));

		var handler = fixture.Services.GetRequiredService<UpdateTimeWindowHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new UpdateTimeWindowRequest(
			new TimeOnly(12, 0),
			new TimeOnly(14, 0),
			"UTC",
			new List<DayOfWeek> { DayOfWeek.Wednesday },
			false,
			"Update with cache clear");

		// Act
		await handler.HandleAsync("cached-window-flag", headers, request, CancellationToken.None);

		// Assert
		var cached = await fixture.Cache.GetAsync(cacheKey);
		cached.ShouldBeNull();
	}

	[Fact]
	public async Task Should_return_404_when_flag_not_found()
	{
		// Arrange
		var handler = fixture.Services.GetRequiredService<UpdateTimeWindowHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var request = new UpdateTimeWindowRequest(
			new TimeOnly(9, 0),
			new TimeOnly(17, 0),
			"UTC",
			new List<DayOfWeek> { DayOfWeek.Monday },
			false,
			"Non-existent flag");

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
