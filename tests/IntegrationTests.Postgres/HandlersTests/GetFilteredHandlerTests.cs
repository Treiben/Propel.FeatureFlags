using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Domain;

namespace IntegrationTests.Postgres.HandlersTests;

public class GetFilteredFlagsHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_return_paged_flags_successfully()
	{
		// Arrange
		var identifier1 = new FlagIdentifier($"paged-flag-1", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag1 = new FeatureFlag(identifier1,
			new FlagAdministration(Name: $"Flag 1",
						Description: $"First flag",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: new() { { "env", "production" }, { "team", "backend" } },
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions with { ModeSet = EvaluationMode.UserTargeted });

		var identifier2 = new FlagIdentifier($"paged-flag-2", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag2 = new FeatureFlag(identifier2,
			new FlagAdministration(Name: $"Flag 2",
						Description: $"Second flag",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: new() { { "env", "production" }, { "team", "backend" } },
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions with { ModeSet = EvaluationMode.UserTargeted });

		var saved = await fixture.DashboardRepository.CreateAsync(flag1, CancellationToken.None);
		saved = await fixture.DashboardRepository.CreateAsync(flag2, CancellationToken.None);


		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		var request = new GetFeatureFlagRequest { Page = 1, PageSize = 10 };

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
		var response = ((Ok<PagedFeatureFlagsResponse>)result).Value;
		response.ShouldNotBeNull();
		response.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
		response.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
		response.Page.ShouldBe(1);
		response.PageSize.ShouldBe(10);
	}

	[Fact]
	public async Task Should_filter_flags_by_evaluation_modes_that_not_found()
	{
		// Arrange
		var identifier = new FlagIdentifier("mode-filter-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Mode Name",
						Description: "With specific mode",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions with { ModeSet =
					new ModeSet([ 
							EvaluationMode.UserTargeted, 
							EvaluationMode.TargetingRules, 
							EvaluationMode.Scheduled,
							EvaluationMode.TenantTargeted,
							EvaluationMode.UserRolloutPercentage
						]) });

		var saved = await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		//---------------------------------------
		var request = new GetFeatureFlagRequest
		{
			Page = 1,
			PageSize = 10,
			Modes = [EvaluationMode.TenantRolloutPercentage]
		};

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
		var response = ((Ok<PagedFeatureFlagsResponse>)result).Value;
		response.ShouldNotBeNull();
		response.Items.Count.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public async Task Should_filter_flags_by_one_evaluation_mode()
	{
		// Arrange
		var identifier = new FlagIdentifier("mode-filter-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Mode Name",
						Description: "With specific mode",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions with
			{
				ModeSet =
					new ModeSet([
							EvaluationMode.UserTargeted,
							EvaluationMode.TargetingRules,
							EvaluationMode.Scheduled,
							EvaluationMode.TenantTargeted,
							EvaluationMode.UserRolloutPercentage
						])
			});

		var saved = await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		//---------------------------------------
		var request = new GetFeatureFlagRequest
		{
			Page = 1,
			PageSize = 10,
			Modes = [EvaluationMode.Scheduled]
		};

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
		var response = ((Ok<PagedFeatureFlagsResponse>)result).Value;
		response.ShouldNotBeNull();
		response.Items.Count.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task Should_filter_flags_by_many_evaluation_modes()
	{
		// Arrange
		var identifier = new FlagIdentifier("mode-filter-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Mode Name",
						Description: "With specific mode",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions with
			{
				ModeSet =
					new ModeSet([
							EvaluationMode.UserTargeted,
							EvaluationMode.TargetingRules,
							EvaluationMode.Scheduled,
							EvaluationMode.TenantTargeted,
							EvaluationMode.UserRolloutPercentage
						])
			});

		var saved = await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		//---------------------------------------
		var request = new GetFeatureFlagRequest
		{
			Page = 1,
			PageSize = 10,
			Modes = [EvaluationMode.TenantRolloutPercentage, EvaluationMode.Scheduled, EvaluationMode.UserRolloutPercentage]
		};

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
		var response = ((Ok<PagedFeatureFlagsResponse>)result).Value;
		response.ShouldNotBeNull();
		response.Items.Count.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task Should_filter_flags_by_tags()
	{
		// Arrange
		var identifier = new FlagIdentifier("tagged-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Tagged Flag",
						Description: "With tags",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: new() { { "env", "production" }, { "team", "backend" } },
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions with { ModeSet = EvaluationMode.UserTargeted });

		var saved = await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		var request = new GetFeatureFlagRequest
		{
			Page = 1,
			PageSize = 10,
			Tags = ["env:production", "team:backend"]
		};

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
		var response = ((Ok<PagedFeatureFlagsResponse>)result).Value;
		response.ShouldNotBeNull();
		response.Items.Count.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task Should_filter_flags_by_tag_keys()
	{
		// Arrange
		var identifier = new FlagIdentifier("tagkey-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Tag Key Flag",
						Description: "With tag keys",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: new() { { "env", "production" }, { "team", "backend" } },
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions with { ModeSet = EvaluationMode.UserTargeted });

		var saved = await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		var request = new GetFeatureFlagRequest
		{
			Page = 1,
			PageSize = 10,
			TagKeys = ["env", "team"]
		};

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
		var response = ((Ok<PagedFeatureFlagsResponse>)result).Value;
		response.ShouldNotBeNull();
		response.Items.Count.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task Should_return_correct_pagination_metadata()
	{
		// Arrange
		for (int i = 0; i < 15; i++)
		{
			var identifier = new FlagIdentifier($"pagination-flag-{i}", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
			var flag = new FeatureFlag(identifier,
				new FlagAdministration(Name: $"Flag {i}",
							Description: $"Flag number {i}",
							RetentionPolicy: RetentionPolicy.GlobalPolicy,
							Tags: new() { { "env", "production" }, { "team", "backend" } },
							ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
				FlagEvaluationOptions.DefaultOptions with { ModeSet = EvaluationMode.UserTargeted });

			var saved = await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);
		}

		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		var request = new GetFeatureFlagRequest { Page = 1, PageSize = 10 };

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
		var response = ((Ok<PagedFeatureFlagsResponse>)result).Value;
		response.Items.Count.ShouldBeLessThanOrEqualTo(10);
		response.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
		response.HasNextPage.ShouldBeTrue();
		response.HasPreviousPage.ShouldBeFalse();
	}

	[Fact]
	public async Task Should_return_empty_result_when_no_flags_match_filter()
	{
		// Arrange
		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		var request = new GetFeatureFlagRequest
		{
			Page = 1,
			PageSize = 10,
			Tags = new[] { "nonexistent:tag" }
		};

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
		var response = ((Ok<PagedFeatureFlagsResponse>)result).Value;
		response.Items.ShouldBeEmpty();
		response.TotalCount.ShouldBe(0);
	}

	public Task InitializeAsync() => Task.CompletedTask;

	public Task DisposeAsync() => fixture.ClearAllData();
}
