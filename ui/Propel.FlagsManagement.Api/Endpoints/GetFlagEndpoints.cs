using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record FeatureFlagResponse
{
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public FlagEvaluationMode[] EvaluationModes { get; set; } = [];
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }
	public string CreatedBy { get; set; } = string.Empty;
	public string? UpdatedBy { get; set; } = string.Empty;
	public DateTime? ExpirationDate { get; set; }
	public DateTime? ScheduledEnableDate { get; set; }
	public DateTime? ScheduledDisableDate { get; set; }
	public TimeOnly? WindowStartTime { get; set; }
	public TimeOnly? WindowEndTime { get; set; }
	public string? TimeZone { get; set; }
	public DayOfWeek[]? WindowDays { get; set; }
	public int PercentageEnabled { get; set; }
	public List<TargetingRule> TargetingRules { get; set; } = [];
	public List<string> AllowedUsers { get; set; } = [];
	public List<string> BlockedUsers { get; set; } = [];
	public Dictionary<string, object> Variations { get; set; } = [];
	public string DefaultVariation { get; set; } = string.Empty;
	public Dictionary<string, string> Tags { get; set; } = [];
	public bool IsPermanent { get; set; }

	public FeatureFlagResponse() { }

	public FeatureFlagResponse(FeatureFlag flag)
	{
		Key = flag.Key;
		Name = flag.Name;
		Description = flag.Description;
		EvaluationModes = flag.EvaluationModeSet.EvaluationModes;
		CreatedAt = flag.AuditRecord.CreatedAt;
		UpdatedAt = flag.AuditRecord.ModifiedAt;
		CreatedBy = flag.AuditRecord.CreatedBy;
		UpdatedBy = flag.AuditRecord.ModifiedBy;
		ExpirationDate = flag.ExpirationDate;
		ScheduledEnableDate = flag.Schedule.ScheduledEnableUtcDate;
		ScheduledDisableDate = flag.Schedule.ScheduledDisableUtcDate;
		WindowStartTime = flag.OperationalWindow.WindowStartTime > TimeSpan.Zero ? TimeOnly.FromTimeSpan(flag.OperationalWindow.WindowStartTime) : null;
		WindowEndTime = flag.OperationalWindow.WindowEndTime > TimeSpan.Zero ? TimeOnly.FromTimeSpan(flag.OperationalWindow.WindowEndTime) : null;
		TimeZone = flag.OperationalWindow.TimeZone;
		WindowDays = flag.OperationalWindow.WindowDays;
		PercentageEnabled = flag.UserAccess.RolloutPercentage;
		AllowedUsers = flag.UserAccess.AllowedUsers;
		BlockedUsers = flag.UserAccess.BlockedUsers;
		TargetingRules = flag.TargetingRules;
		Variations = flag.Variations.Values;
		DefaultVariation = flag.Variations.DefaultVariation;
		Tags = flag.Tags;
		IsPermanent = flag.IsPermanent;
	}
}

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
	public FlagEvaluationMode[]? EvaluationModes { get; init; }

	// Expiring flags only
	public int? ExpiringInDays { get; init; }

	// Tag filtering using tag keys only
	public string[]? TagKeys { get; init; }

	// Tag filtering by suing tags in key:value format
	public string[]? Tags { get; init; }
}

public sealed class GetFlagEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/feature-flags/{key}",
			async (string key,
					IFeatureFlagRepository repository, 
					ILogger<GetFlagEndpoints> logger) =>
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

				return Results.Ok(new FeatureFlagResponse(flag));
			}
			catch (Exception ex)
			{
				return HttpProblemFactory.InternalServerError(ex, logger);
			}
		})
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
		.WithName("GetFlag")
		.WithTags("Feature Flags", "CRUD Operations", "Read", "Management Api")
		.Produces<FeatureFlagResponse>();

		app.MapGet("/api/feature-flags/all", 
			async (IFeatureFlagRepository repository, 
					ILogger<GetFlagEndpoints> logger) =>
		{
			try
			{
				var flags = await repository.GetAllAsync();
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
		.WithTags("Feature Flags", "CRUD Operations", "Read", "Management Api", "All Flags")
		.Produces<List<FeatureFlagResponse>>();

		app.MapGet("/api/feature-flags", 
			async ([AsParameters] GetFeatureFlagRequest request, 
					IFeatureFlagRepository repository,
					ILogger<GetFlagEndpoints> logger) =>
		{
			try
			{
				FeatureFlagFilter? filter = null;
				if (request.EvaluationModes?.Length > 0 || request.Tags?.Length > 0 || request.TagKeys?.Length > 0)
				{
					filter = new FeatureFlagFilter
					{
						EvaluationModes = request.EvaluationModes,
						Tags = request.BuildTagDictionary(),
						ExpiringInDays = request.ExpiringInDays
					};
				}

				var result = await repository.GetPagedAsync(request.Page ?? 1,
						request.PageSize ?? 10
						, filter);

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
			catch (Exception ex)
			{
				return HttpProblemFactory.InternalServerError(ex, logger);
			}
		})
		.AddEndpointFilter<ValidationFilter<GetFeatureFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
		.WithName("GetFlagsWithPageOrFilter")
		.WithTags("Feature Flags", "CRUD Operations", "Read", "Management Api", "Paging, Filtering")
		.Produces<PagedFeatureFlagsResponse>();
	}
}

public static class GetFlagsRequestExtensions
{
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