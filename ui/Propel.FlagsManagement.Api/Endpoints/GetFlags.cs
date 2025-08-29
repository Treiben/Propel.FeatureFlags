using FluentValidation;
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

public sealed class GetFlagsEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/flags", async (IFeatureFlagRepository repository, ILogger<GetFlagsEndpoint> logger) =>
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
	}
}

public sealed class GetFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/flags/{key}", async (string key, IFeatureFlagRepository repository, ILogger<GetFlagEndpoint> logger) =>
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
	}
}