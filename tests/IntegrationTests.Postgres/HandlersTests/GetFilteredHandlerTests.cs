using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Domain;

namespace IntegrationTests.Postgres.HandlersTests;

public class GetFilteredFlagsHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_return_paged_flags_successfully()
	{
		// Arrange
		var flag1 = FlagEvaluationConfiguration.CreateGlobal("paged-flag-1");
		var flag2 = FlagEvaluationConfiguration.CreateGlobal("paged-flag-2");
		await fixture.SaveAsync(flag1, "Flag 1", "First flag");
		await fixture.SaveAsync(flag2, "Flag 2", "Second flag");

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
	public async Task Should_filter_flags_by_evaluation_modes()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("mode-filter-flag");
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.On);
		await fixture.SaveAsync(flag, "Mode Flag", "With specific mode");

		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		var request = new GetFeatureFlagRequest
		{
			Page = 1,
			PageSize = 10,
			Modes = new[] { EvaluationMode.On }
		};

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
		var response = ((Ok<PagedFeatureFlagsResponse>)result).Value;
		response.ShouldNotBeNull();
	}

	[Fact]
	public async Task Should_filter_flags_by_tags()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("tagged-flag");
		await fixture.SaveAsync(flag, "Tagged Flag", "With tags");

		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		var request = new GetFeatureFlagRequest
		{
			Page = 1,
			PageSize = 10,
			Tags = new[] { "env:production", "team:backend" }
		};

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
	}

	[Fact]
	public async Task Should_filter_flags_by_tag_keys()
	{
		// Arrange
		var flag = FlagEvaluationConfiguration.CreateGlobal("tagkey-flag");
		await fixture.SaveAsync(flag, "Tag Key Flag", "With tag keys");

		var handler = fixture.Services.GetRequiredService<GetFilteredFlagsHandler>();
		var request = new GetFeatureFlagRequest
		{
			Page = 1,
			PageSize = 10,
			TagKeys = new[] { "env", "team" }
		};

		// Act
		var result = await handler.HandleAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<PagedFeatureFlagsResponse>>();
	}

	[Fact]
	public async Task Should_return_correct_pagination_metadata()
	{
		// Arrange
		for (int i = 0; i < 15; i++)
		{
			var flag = FlagEvaluationConfiguration.CreateGlobal($"pagination-flag-{i}");
			await fixture.SaveAsync(flag, $"Flag {i}", $"Flag number {i}");
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
