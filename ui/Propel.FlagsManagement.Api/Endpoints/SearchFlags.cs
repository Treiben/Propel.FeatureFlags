using Propel.FeatureFlags;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public sealed class SearchFlagsEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapGet("/api/feature-flags/search",
			async (
				string? tag,
				string? status,
				SearchHandler searchHandler) =>
			{
				return await searchHandler.HandleAsync(tag, status);
			})
			.WithName("SearchFlags")
			.WithTags("Feature Flags", "Search", "Management Api")
			.Produces<List<FeatureFlagDto>>()
			.AllowAnonymous();
	}
}

public sealed class SearchHandler(
	IFeatureFlagRepository repository,
	ILogger<SearchHandler> logger)
{
	public async Task<IResult> HandleAsync(string? tag = null, string? status = null)
	{
		// Validate tag format if provided
		if (!string.IsNullOrEmpty(tag))
		{
			if (tag.Length > 100)
			{
				return HttpProblemFactory.BadRequest(
					"Invalid Tag Parameter",
					"Tag parameter cannot exceed 100 characters",
					logger);
			}

			// Validate tag format (key:value or just key)
			var tagParts = tag.Split(':', 2);
			if (tagParts[0].Length == 0)
			{
				return HttpProblemFactory.BadRequest(
					"Invalid Tag Format",
					"Tag parameter must be in format 'key' or 'key:value'",
					logger);
			}
		}

		// Validate status if provided
		if (!string.IsNullOrWhiteSpace(status) && !IsValidStatus(status, out _))
		{
			var validStatuses = string.Join(", ", Enum.GetNames<FeatureFlagStatus>());
			return HttpProblemFactory.BadRequest(
				"Invalid Status Parameter",
				$"Status must be one of: {validStatuses}",
				logger);
		}

		try
		{
			List<FeatureFlag> flags;

			if (!string.IsNullOrEmpty(tag))
			{
				var tagParts = tag.Split(':', 2);
				var tagKey = tagParts[0];
				var tagValue = tagParts.Length > 1 ? tagParts[1] : "";
				flags = await repository.GetByTagsAsync(new Dictionary<string, string> { [tagKey] = tagValue });
			}
			else
			{
				flags = await repository.GetAllAsync();
			}

			if (IsValidStatus(status, out var statusEnum))
			{
				flags = [.. flags.Where(f => f.Status == statusEnum)];
			}

			var flagDtos = flags.Select(f => new FeatureFlagDto(f)).ToList();
			
			logger.LogInformation("Search completed: found {Count} flags with tag='{Tag}', status='{Status}'", 
				flagDtos.Count, tag ?? "any", status ?? "any");
			
			return Results.Ok(flagDtos);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}

	private static bool IsValidStatus(string? status, out FeatureFlagStatus statusEnum)
	{
		statusEnum = FeatureFlagStatus.Disabled; // Default value

		return Enum.TryParse(status, true, out statusEnum);
	}
}


