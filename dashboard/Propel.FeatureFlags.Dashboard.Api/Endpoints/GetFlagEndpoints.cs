using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public record PagedFeatureFlagsResponse
{
	public List<FeatureFlagResponse> Items { get; init; } = [];
	public int TotalCount { get; init; }
	public int Page { get; init; }
	public int PageSize { get; init; }
	public int TotalPages { get; init; }
	public bool HasNextPage { get; init; }
	public bool HasPreviousPage { get; init; }
}

public record GetFeatureFlagRequest
{
	public int? Page { get; init; }
	public int? PageSize { get; init; }

	// Evaluation mode filtering
	public EvaluationMode[]? Modes { get; init; }

	// Expiring flags only
	public int? ExpiringInDays { get; init; }

	// Tag filtering using tag keys only
	public string[]? TagKeys { get; init; }

	// Tag filtering by using tags in key:value format
	public string[]? Tags { get; init; }

	public Scope? Scope { get; init; }

	public string? ApplicationName { get; init; }
}

public sealed class GetFlagEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/feature-flags/{key}",
			async (string key,
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				IFlagResolverService flagResolver,
				ILogger<GetFlagEndpoints> logger,
				CancellationToken cancellationToken) =>
			{
				try
				{
					var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, 
							new FlagRequestHeaders(scope, applicationName, applicationVersion), 
							cancellationToken);

					if (!isValid)
						return result;

					return Results.Ok(new FeatureFlagResponse(flag!));
				}
				catch (Exception ex)
				{
					return HttpProblemFactory.InternalServerError(ex, logger);
				}
			})
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
		.WithName("GetFlag")
		.WithTags("Feature Flags", "CRUD Operations", "Read", "Dashboard Api")
		.Produces<FeatureFlagResponse>();

		app.MapGet("/api/feature-flags/all",
			async (IDashboardRepository repository,
					ILogger<GetFlagEndpoints> logger,
					CancellationToken cancellationToken) =>
			{
				try
				{
					var flags = await repository.GetAllAsync(cancellationToken);
					var flagDtos = flags.Select(f => new FeatureFlagResponse(f)).ToList();
					return Results.Ok(flagDtos);
				}
				catch (Exception ex)
				{
					return HttpProblemFactory.InternalServerError(ex, logger);
				}
			})
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
		.WithName("GetAllFlags")
		.WithTags("Feature Flags", "CRUD Operations", "Read", "Dashboard Api", "All Flags")
		.Produces<List<FeatureFlagResponse>>();

		app.MapGet("/api/feature-flags",
			async ([AsParameters] GetFeatureFlagRequest request,
					GetFilteredFlagsHandler handler,
					CancellationToken cancellationToken) =>
			{
				return await handler.HandleAsync(request, cancellationToken);
			})
		.AddEndpointFilter<ValidationFilter<GetFeatureFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
		.WithName("GetFlagsWithPageOrFilter")
		.WithTags("Feature Flags", "CRUD Operations", "Read", "Dashboard Api", "Paging, Filtering")
		.Produces<PagedFeatureFlagsResponse>();
	}
}


public sealed class GetFilteredFlagsHandler(IDashboardRepository repository, ILogger<GetFilteredFlagsHandler> logger)
{
	public async Task<IResult> HandleAsync(GetFeatureFlagRequest request, CancellationToken cancellationToken)
	{
		try
		{
			FeatureFlagFilter? filter = null;
			if (request.FilteringRequested())
			{
				filter = new FeatureFlagFilter(
					EvaluationModes: request.Modes,
					Tags: request.BuildTagDictionary(),
					Scope: request.Scope,
					ApplicationName: request.ApplicationName);
			}

			var result = await repository.GetPagedAsync(
				request.Page ?? 1,
				request.PageSize ?? 10,
				filter,
				cancellationToken);

			var response = new PagedFeatureFlagsResponse
			{
				Items = [.. result.Items.Select(f => new FeatureFlagResponse(f))],
				TotalCount = result.TotalCount,
				Page = result.Page,
				PageSize = result.PageSize,
				TotalPages = result.TotalPages,
				HasNextPage = result.HasNextPage,
				HasPreviousPage = result.HasPreviousPage
			};

			return Results.Ok(response);
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

public static class GetFlagsRequestExtensions
{
	public static bool FilteringRequested(this GetFeatureFlagRequest request)
	{
		return (request.Modes != null && request.Modes.Length > 0) ||
			   (request.TagKeys != null && request.TagKeys.Length > 0) ||
			   (request.Tags != null && request.Tags.Length > 0) ||
			   (request.TagKeys != null && request.TagKeys.Length > 0) ||
				request.Scope != null ||
				!string.IsNullOrWhiteSpace(request.ApplicationName);
	}
	public static Dictionary<string, string>? BuildTagDictionary(this GetFeatureFlagRequest request)
	{
		var tags = new Dictionary<string, string>();
		// Handle Tags array (key:value format)
		if (request.Tags != null)
		{
			foreach (var tag in request.Tags)
			{
				var parts = tag.Split(':', 2);
				if (parts.Length == 2)
				{
					var key = parts[0].Trim();
					var value = parts[1].Trim();
					if (!string.IsNullOrEmpty(key))
					{
						tags[key] = value;
					}
				}
				else if (parts.Length == 1)
				{
					var key = parts[0].Trim();
					if (!string.IsNullOrEmpty(key))
					{
						tags[key] = "";
					}
				}
			}
		}
		// Handle TagKeys
		if (request.TagKeys != null)
		{
			for (int i = 0; i < request.TagKeys.Length; i++)
			{
				var key = request.TagKeys[i].Trim();
				if (!string.IsNullOrEmpty(key))
				{
					tags[key] = "";
				}
			}
		}
		return tags.Count > 0 ? tags : null;
	}
}

public class GetFlagsRequestValidator : AbstractValidator<GetFeatureFlagRequest>
{
	public GetFlagsRequestValidator()
	{
		RuleFor(x => x.Page)
			.GreaterThan(0)
			.WithMessage("Page must be greater than 0");

		RuleFor(x => x.PageSize)
			.InclusiveBetween(1, 100)
			.WithMessage("Page size must be between 1 and 100");

		RuleFor(x => x.Tags)
			.Must(BeValidTagFormat)
			.When(x => x.Tags != null && x.Tags.Length > 0)
			.WithMessage("Tags must be in format 'key:value' or just 'key' for key-only searches");

		RuleFor(x => x.ExpiringInDays)
			.InclusiveBetween(1, 365)
			.When(x => x.ExpiringInDays.HasValue)
			.WithMessage("ExpiringInDays must be between 1 and 365");
	}

	private static bool BeValidTagFormat(string[]? tags)
	{
		if (tags == null) return true;

		return tags.All(tag =>
		{
			if (string.IsNullOrWhiteSpace(tag)) return false;

			// Allow format "key:value" or just "key"
			var parts = tag.Split(':', 2);
			return parts.Length is 1 or 2 && !string.IsNullOrWhiteSpace(parts[0]);
		});
	}
}