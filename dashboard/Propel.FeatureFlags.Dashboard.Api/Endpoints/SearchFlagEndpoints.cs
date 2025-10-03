using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public record SearchFeatureFlagRequest
{
	public string? Key { get; init; }

	public string? Name { get; init; }

	public string? Description { get; init; }
}

public sealed class SearchFlagEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/feature-flags/search",
			async ([AsParameters] SearchFeatureFlagRequest request,
					SearchFeatureFlagsHandler handler,
					CancellationToken cancellationToken) =>
			{
				return await handler.HandleAsync(request, cancellationToken);
			})
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
		.WithName("SearchFeatureFlags")
		.WithTags("Feature Flags", "CRUD Operations", "Read", "Dashboard Api", "Search")
		.Produces<List<FeatureFlagResponse>>();
	}
}


public sealed class SearchFeatureFlagsHandler(IDashboardRepository repository, ILogger<SearchFeatureFlagsHandler> logger)
{
	public async Task<IResult> HandleAsync(SearchFeatureFlagRequest request, CancellationToken cancellationToken)
	{
		try
		{
			FindFlagCriteria? criteria = null;
			if (request.Key != null || request.Name != null || request.Description != null)
			{
				criteria = new FindFlagCriteria(
					Key: request.Key,
					Name: request.Name,
					Description: request.Description);
			}
			
			if (criteria == null)
			{
				return Results.BadRequest(new ProblemDetails
				{
					Title = "At least one search parameter must be provided",
					Status = StatusCodes.Status400BadRequest,
					Detail = "Please provide at least one of the following parameters: Key, Name, Description."
				});
			}

			var flags = await repository.FindAsync(criteria, cancellationToken);
			var responses = flags.Select(f => new FeatureFlagResponse(f)).ToList();
			return Results.Ok(responses);
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest("Invalid request parameter", ex.Message, logger);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}