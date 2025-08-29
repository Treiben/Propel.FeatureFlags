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
			.WithTags("Feature Flags", "Search", "Management")
			.Produces<Dictionary<string, FeatureFlagDto>>()
			.AllowAnonymous();
	}
}

public sealed class SearchHandler(
	IFeatureFlagRepository repository,
	ILogger<SearchHandler> logger)
{
	public async Task<IResult> HandleAsync(string? tag = null, string? status = null)
	{
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
			return Results.Ok(flagDtos);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error searching flags");
			return Results.StatusCode(500);
		}
	}

	private static bool IsValidStatus(string status, out FeatureFlagStatus statusEnum)
	{
		bool isValidStatus = !string.IsNullOrEmpty(status) 
			&& Enum.GetNames<FeatureFlagStatus>().Any(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));

		if (isValidStatus &&  Enum.TryParse(status, true, out statusEnum))
		{
			return true;
		}
		statusEnum = FeatureFlagStatus.Disabled; // Default value if parsing fails
		return false;
	}
}


