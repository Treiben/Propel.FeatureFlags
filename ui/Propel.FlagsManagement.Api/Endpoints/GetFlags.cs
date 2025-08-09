using FeatureRabbit.Flags.Core;
using FeatureRabbit.Management.Api.Endpoints.Shared;
using FeatureRabbit.Flags.Persistence;
using FluentValidation;

namespace FeatureRabbit.Management.Api.Endpoints;

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
	public List<TargetingRule> TargetingRules { get; set; } = new();
	public List<string> EnabledUsers { get; set; } = new();
	public List<string> DisabledUsers { get; set; } = new();
	public Dictionary<string, object> Variations { get; set; } = new();
	public string DefaultVariation { get; set; } = string.Empty;
	public Dictionary<string, string> Tags { get; set; } = new();
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

public sealed class FetchFlagsEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/flags", async (IFeatureFlagRepository repository, ILogger<FetchFlagsEndpoint> logger) =>
		{
			try
			{
				var flags = await repository.GetAllAsync();
				var flagDtos = flags.Select(f => new FeatureFlagDto(f)).ToList();
				return Results.Ok(flagDtos);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving feature flags");
				return Results.StatusCode(500);
			}
		})
		.WithName("GetAllFlags")
		.WithTags("Feature Flags", "Management")
		.Produces<Dictionary<string, FeatureFlagDto>>()
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy);
	}
}

public sealed class FetchFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/flags/{key}", async (string key, IFeatureFlagRepository repository, ILogger<FetchFlagEndpoint> logger) =>
		{
			try
			{
				var flag = await repository.GetAsync(key);
				if (flag == null)
					return Results.NotFound($"Feature flag '{key}' not found");

				return Results.Ok(new FeatureFlagDto(flag));
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error retrieving feature flag {Key}", key);
				return Results.StatusCode(500);
			}
		})
		.WithName("GetFlag")
		.WithTags("Feature Flags", "Management")
		.Produces<Dictionary<string, FeatureFlagDto>>()
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy);
	}
}