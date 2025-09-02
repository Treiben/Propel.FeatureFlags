using FluentValidation;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record FeatureFlagDto
{
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
	public string CreatedBy { get; set; } = string.Empty;
	public string UpdatedBy { get; set; } = string.Empty;
	public DateTime? ExpirationDate { get; set; }
	public DateTime? ScheduledEnableDate { get; set; }
	public DateTime? ScheduledDisableDate { get; set; }
	public TimeOnly? WindowStartTime { get; set; }
	public TimeOnly? WindowEndTime { get; set; }
	public string? TimeZone { get; set; }
	public List<DayOfWeek>? WindowDays { get; set; }
	public int PercentageEnabled { get; set; }
	public List<TargetingRule> TargetingRules { get; set; } = [];
	public List<string> EnabledUsers { get; set; } = [];
	public List<string> DisabledUsers { get; set; } = [];
	public Dictionary<string, object> Variations { get; set; } = [];
	public string DefaultVariation { get; set; } = string.Empty;
	public Dictionary<string, string> Tags { get; set; } = [];
	public bool IsPermanent { get; set; }

	public FeatureFlagDto() { }

	public FeatureFlagDto(FeatureFlag flag)
	{
		Key = flag.Key;
		Name = flag.Name;
		Description = flag.Description;
		Status = flag.Status.ToString();
		CreatedAt = flag.CreatedAt;
		UpdatedAt = flag.UpdatedAt;
		CreatedBy = flag.CreatedBy;
		UpdatedBy = flag.UpdatedBy;
		ExpirationDate = flag.ExpirationDate;
		ScheduledEnableDate = flag.ScheduledEnableDate;
		ScheduledDisableDate = flag.ScheduledDisableDate;
		WindowStartTime = flag.WindowStartTime.HasValue ? TimeOnly.FromTimeSpan(flag.WindowStartTime.Value) : null;
		WindowEndTime = flag.WindowEndTime.HasValue ? TimeOnly.FromTimeSpan(flag.WindowEndTime.Value) : null;
		TimeZone = flag.TimeZone;
		WindowDays = flag.WindowDays;
		PercentageEnabled = flag.PercentageEnabled;
		TargetingRules = flag.TargetingRules;
		EnabledUsers = flag.EnabledUsers;
		DisabledUsers = flag.DisabledUsers;
		Variations = flag.Variations;
		DefaultVariation = flag.DefaultVariation;
		Tags = flag.Tags;
		IsPermanent = flag.IsPermanent;
	}
}

public record PagedFeatureFlagsResponse
{
	public List<FeatureFlagDto> Items { get; init; } = [];
	public int TotalCount { get; init; }
	public int Page { get; init; }
	public int PageSize { get; init; }
	public int TotalPages { get; init; }
	public bool HasNextPage { get; init; }
	public bool HasPreviousPage { get; init; }
}

public record GetFlagsRequest
{
	public int? Page { get; init; }
	public int? PageSize { get; init; }
	public string? Status { get; init; }

	// Tag filtering using tag keys only
	public string[]? TagKeys { get; init; }

	// Tag filtering by suing tags in key:value format
	public string[]? Tags { get; init; }
}

public sealed class FetchFlagsEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/feature-flags/{key}", async (string key, IFeatureFlagRepository repository, ILogger<FetchFlagsEndpoints> logger) =>
		{
			// Validate key parameter
			if (string.IsNullOrWhiteSpace(key))
			{
				return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
			}

			try
			{
				var flag = await repository.GetAsync(key);
				if (flag == null)
				{
					return HttpProblemFactory.NotFound("Feature flag", key, logger);
				}

				return Results.Ok(new FeatureFlagDto(flag));
			}
			catch (Exception ex)
			{
				return HttpProblemFactory.InternalServerError(ex, logger);
			}
		})
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
		.WithName("GetFlag")
		.WithTags("Feature Flags", "CRUD Operations", "Read", "Management Api")
		.Produces<FeatureFlagDto>();

		app.MapGet("/api/feature-flags/all", async (IFeatureFlagRepository repository, ILogger<FetchFlagsEndpoints> logger) =>
		{
			try
			{
				var flags = await repository.GetAllAsync();
				var flagDtos = flags.Select(f => new FeatureFlagDto(f)).ToList();
				return Results.Ok(flagDtos);
			}
			catch (Exception ex)
			{
				return HttpProblemFactory.InternalServerError(ex, logger);
			}
		})
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
		.WithName("GetAllFlags")
		.WithTags("Feature Flags", "CRUD Operations", "Read", "Management Api")
		.Produces<List<FeatureFlagDto>>();

		app.MapGet("/api/feature-flags", async ([AsParameters] GetFlagsRequest request, IFeatureFlagRepository repository, ILogger<FetchFlagsEndpoints> logger) =>
		{
			try
			{
				FeatureFlagFilter? filter = null;
				if (!string.IsNullOrEmpty(request.Status) || request.Tags?.Length > 0 || request.TagKeys?.Length > 0)
				{
					filter = new FeatureFlagFilter
					{
						Status = request.Status,
						Tags = request.BuildTagDictionary()
					};
				}

				var result = await repository.GetPagedAsync(request.Page ?? 1,
						request.PageSize ?? 10
						, filter);

				var response = new PagedFeatureFlagsResponse
				{
					Items = [.. result.Items.Select(f => new FeatureFlagDto(f))],
					TotalCount = result.TotalCount,
					Page = result.Page,
					PageSize = result.PageSize,
					TotalPages = result.TotalPages,
					HasNextPage = result.HasNextPage,
					HasPreviousPage = result.HasPreviousPage
				};

				return Results.Ok(response);
			}
			catch (Exception ex)
			{
				return HttpProblemFactory.InternalServerError(ex, logger);
			}
		})
		.AddEndpointFilter<ValidationFilter<GetFlagsRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
		.WithName("GetPagedFlags")
		.WithTags("Feature Flags", "CRUD Operations", "Read", "Management Api", "Paging")
		.Produces<PagedFeatureFlagsResponse>();
	}
}

public static class GetFlagsRequestExtensions
{
	public static Dictionary<string, string>? BuildTagDictionary(this GetFlagsRequest request)
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

public class GetFlagsRequestValidator : AbstractValidator<GetFlagsRequest>
{
	public GetFlagsRequestValidator()
	{
		RuleFor(x => x.Page)
			.GreaterThan(0)
			.WithMessage("Page must be greater than 0");

		RuleFor(x => x.PageSize)
			.InclusiveBetween(1, 100)
			.WithMessage("Page size must be between 1 and 100");

		RuleFor(x => x.Status)
			.Must(BeValidStatus)
			.When(x => !string.IsNullOrEmpty(x.Status))
			.WithMessage("Status must be one of: Disabled, Enabled, Scheduled, TimeWindow, UserTargeted, Percentage");

		RuleFor(x => x.Tags)
			.Must(BeValidTagFormat)
			.When(x => x.Tags != null && x.Tags.Length > 0)
			.WithMessage("Tags must be in format 'key:value' or just 'key' for key-only searches");
	}

	private static bool BeValidStatus(string? status)
	{
		if (string.IsNullOrEmpty(status)) return true;
		return Enum.TryParse<FeatureFlagStatus>(status, true, out _);
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