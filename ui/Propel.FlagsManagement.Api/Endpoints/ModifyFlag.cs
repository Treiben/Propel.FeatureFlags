using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record ModifyFlagRequest
{
	public string? Name { get; set; }
	public string? Description { get; set; }
	public FeatureFlagStatus? Status { get; set; }
	public DateTime? ExpirationDate { get; set; }
	public DateTime? ScheduledEnableDate { get; set; }
	public DateTime? ScheduledDisableDate { get; set; }
	public TimeOnly? WindowStartTime { get; set; }
	public TimeOnly? WindowEndTime { get; set; }
	public string? TimeZone { get; set; }
	public List<DayOfWeek>? WindowDays { get; set; }
	public int? PercentageEnabled { get; set; }
	public List<TargetingRule>? TargetingRules { get; set; }
	public List<string>? EnabledUsers { get; set; }
	public List<string>? DisabledUsers { get; set; }
	public Dictionary<string, object>? Variations { get; set; }
	public string? DefaultVariation { get; set; }
	public Dictionary<string, string>? Tags { get; set; }
	public bool? IsPermanent { get; set; }
}
public sealed class ModifyFlag : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPut("/api/feature-flags/{key}",
			async (string key, 
					ModifyFlagRequest request,
					ModifyFlagHandler handler) =>
		{
			return await handler.HandleAsync(key, request);
		})
		.AddEndpointFilter<ValidationFilter<ModifyFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("UpdateFeatureFlag")
		.WithTags("Feature Flags", "Management");
	}

	public sealed class ModifyFeatureFlagRequestValidator : AbstractValidator<ModifyFlagRequest>
	{
		public ModifyFeatureFlagRequestValidator()
		{
			RuleFor(c => c.Name)
				.MaximumLength(200)
				.When(c => !string.IsNullOrEmpty(c.Name))
				.WithMessage("Stop name length must be greater than 0 and less than 200.");
			RuleFor(c => c.PercentageEnabled)
				.Must(p => p >= 0 && p <= 100)
				.WithMessage("Feature flag rollout percentage must be between 0 and 100.");
		}
	}
}

public sealed class ModifyFlagHandler(CurrentUserService userService,
					IFeatureFlagRepository repository,
					IFeatureFlagCache cache,
					ILogger<ModifyFlag> logger)
{
	public async Task<IResult> HandleAsync(string key, ModifyFlagRequest request)
	{
		try
		{
			var existingFlag = await repository.GetAsync(key);
			if (existingFlag == null)
				return Results.NotFound($"Feature flag '{key}' not found");
			// Update fields
			existingFlag.Name = request.Name ?? existingFlag.Name;
			existingFlag.Description = request.Description ?? existingFlag.Description;
			existingFlag.Status = request.Status ?? existingFlag.Status;
			existingFlag.UpdatedBy = userService.UserName;
			existingFlag.ExpirationDate = request.ExpirationDate ?? existingFlag.ExpirationDate;
			existingFlag.ScheduledEnableDate = request.ScheduledEnableDate ?? existingFlag.ScheduledEnableDate;
			existingFlag.ScheduledDisableDate = request.ScheduledDisableDate ?? existingFlag.ScheduledDisableDate;
			existingFlag.WindowStartTime = request.WindowStartTime?.ToTimeSpan() ?? existingFlag.WindowStartTime;
			existingFlag.WindowEndTime = request.WindowEndTime?.ToTimeSpan() ?? existingFlag.WindowEndTime;
			existingFlag.TimeZone = request.TimeZone ?? existingFlag.TimeZone;
			existingFlag.WindowDays = request.WindowDays ?? existingFlag.WindowDays;
			existingFlag.PercentageEnabled = request.PercentageEnabled ?? existingFlag.PercentageEnabled;
			existingFlag.TargetingRules = request.TargetingRules ?? existingFlag.TargetingRules;
			existingFlag.EnabledUsers = request.EnabledUsers ?? existingFlag.EnabledUsers;
			existingFlag.DisabledUsers = request.DisabledUsers ?? existingFlag.DisabledUsers;
			existingFlag.Variations = request.Variations ?? existingFlag.Variations;
			existingFlag.DefaultVariation = request.DefaultVariation ?? existingFlag.DefaultVariation;
			existingFlag.Tags = request.Tags ?? existingFlag.Tags;
			existingFlag.IsPermanent = request.IsPermanent ?? existingFlag.IsPermanent;

			await repository.UpdateAsync(existingFlag);
			await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} updated by {User}", key, userService.UserName);
			return Results.Ok(new FeatureFlagDto(existingFlag));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error updating feature flag {Key}", key);
			return Results.StatusCode(500);
		}
	}
}
