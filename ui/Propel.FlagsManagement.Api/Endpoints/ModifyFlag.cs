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
		.WithTags("Feature Flags", "CRUD Operations", "Update", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();
	}
}

public sealed class ModifyFlagHandler(
	CurrentUserService userService,
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<ModifyFlagHandler> logger)
{
	public async Task<IResult> HandleAsync(string key, ModifyFlagRequest request)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var existingFlag = await repository.GetAsync(key);
			if (existingFlag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			// Apply business validation for the update
			var businessValidationResult = request.ValidateBusinessRules(existingFlag);
			if (!businessValidationResult.IsValid)
			{
				return HttpProblemFactory.ValidationFailed(businessValidationResult.Errors, logger);
			}

			request.Map(existingFlag);
			existingFlag.UpdatedBy = userService.UserName!;
			existingFlag.UpdatedAt = DateTime.UtcNow;

			var updatedFlag = await repository.UpdateAsync(existingFlag);
			await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} updated by {User}", key, userService.UserName);
			return Results.Ok(new FeatureFlagDto(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public static class ModifyFlagRequestExtensions
{
	public static BusinessValidationResult ValidateBusinessRules(this ModifyFlagRequest request, FeatureFlag existingFlag)
	{
		var errors = new Dictionary<string, List<string>>();

		// If status is being changed, validate status-specific requirements
		var targetStatus = request.Status ?? existingFlag.Status;
		
		switch (targetStatus)
		{
			case FeatureFlagStatus.Scheduled:
				var scheduledDate = request.ScheduledEnableDate ?? existingFlag.ScheduledEnableDate;
				if (!scheduledDate.HasValue)
					AddError(errors, nameof(request.ScheduledEnableDate), "Scheduled enable date is required when status is 'Scheduled'");
				break;

			case FeatureFlagStatus.TimeWindow:
				var startTime = request.WindowStartTime?.ToTimeSpan() ?? existingFlag.WindowStartTime;
				var endTime = request.WindowEndTime?.ToTimeSpan() ?? existingFlag.WindowEndTime;
				var windowDays = request.WindowDays ?? existingFlag.WindowDays;

				if (!startTime.HasValue || !endTime.HasValue)
					AddError(errors, nameof(request.WindowStartTime), "Window start and end times are required when status is 'TimeWindow'");
				if (windowDays == null || windowDays.Count == 0)
					AddError(errors, nameof(request.WindowDays), "Window days are required when status is 'TimeWindow'");
				break;

			case FeatureFlagStatus.Percentage:
				var percentage = request.PercentageEnabled ?? existingFlag.PercentageEnabled;
				if (percentage <= 0)
					AddError(errors, nameof(request.PercentageEnabled), "Percentage enabled must be greater than 0 when status is 'Percentage'");
				break;

			case FeatureFlagStatus.UserTargeted:
				var enabledUsers = request.EnabledUsers ?? existingFlag.EnabledUsers;
				var targetingRules = request.TargetingRules ?? existingFlag.TargetingRules;
				
				if ((enabledUsers == null || enabledUsers.Count == 0) &&
					(targetingRules == null || targetingRules.Count == 0))
					AddError(errors, nameof(request.EnabledUsers), "At least one enabled user or targeting rule is required when status is 'UserTargeted'");
				break;
		}

		// Validate user lists don't overlap (if both are being updated)
		if (request.EnabledUsers != null && request.DisabledUsers != null)
		{
			var overlappingUsers = request.EnabledUsers.Intersect(request.DisabledUsers).ToList();
			if (overlappingUsers.Count > 0)
			{
				AddError(errors, nameof(request.EnabledUsers), $"Users cannot be in both enabled and disabled lists: {string.Join(", ", overlappingUsers)}");
			}
		}

		return new BusinessValidationResult 
		{ 
			IsValid = errors.Count == 0, 
			Errors = errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()) 
		};
	}

	public static void Map(this ModifyFlagRequest source, FeatureFlag dest)
	{
		// Update only non-null properties from the request
		if (source.Name != null) dest.Name = source.Name;
		if (source.Description != null) dest.Description = source.Description;
		if (source.Status.HasValue) dest.Status = source.Status.Value;
		if (source.ExpirationDate.HasValue) dest.ExpirationDate = source.ExpirationDate;
		if (source.ScheduledEnableDate.HasValue) dest.ScheduledEnableDate = source.ScheduledEnableDate;
		if (source.ScheduledDisableDate.HasValue) dest.ScheduledDisableDate = source.ScheduledDisableDate;
		if (source.WindowStartTime.HasValue) dest.WindowStartTime = source.WindowStartTime.Value.ToTimeSpan();
		if (source.WindowEndTime.HasValue) dest.WindowEndTime = source.WindowEndTime.Value.ToTimeSpan();
		if (source.TimeZone != null) dest.TimeZone = source.TimeZone;
		if (source.WindowDays != null) dest.WindowDays = source.WindowDays;
		if (source.PercentageEnabled.HasValue) dest.PercentageEnabled = source.PercentageEnabled.Value;
		if (source.TargetingRules != null) dest.TargetingRules = source.TargetingRules;
		if (source.EnabledUsers != null) dest.EnabledUsers = source.EnabledUsers;
		if (source.DisabledUsers != null) dest.DisabledUsers = source.DisabledUsers;
		if (source.Variations != null) dest.Variations = source.Variations;
		if (source.DefaultVariation != null) dest.DefaultVariation = source.DefaultVariation;
		if (source.Tags != null) dest.Tags = source.Tags;
		if (source.IsPermanent.HasValue) dest.IsPermanent = source.IsPermanent.Value;
	}

	private static void AddError(Dictionary<string, List<string>> errors, string propertyName, string errorMessage)
	{
		if (!errors.TryGetValue(propertyName, out List<string>? value))
		{
			errors[propertyName] = [errorMessage];
		}
		else
		{
			value.Add(errorMessage);
		}
	}
}

public sealed class ModifyFeatureFlagRequestValidator : AbstractValidator<ModifyFlagRequest>
{
	public ModifyFeatureFlagRequestValidator()
	{
		RuleFor(c => c.Name)
			.MaximumLength(200)
			.When(c => !string.IsNullOrEmpty(c.Name))
			.WithMessage("Feature flag name must be between 1 and 200 characters");

		RuleFor(c => c.Description)
			.MaximumLength(1000)
			.When(c => !string.IsNullOrEmpty(c.Description))
			.WithMessage("Feature flag description cannot exceed 1000 characters");

		RuleFor(c => c.PercentageEnabled)
			.InclusiveBetween(0, 100)
			.When(c => c.PercentageEnabled.HasValue)
			.WithMessage("Feature flag rollout percentage must be between 0 and 100");

		RuleFor(c => c.ExpirationDate)
			.GreaterThan(DateTime.UtcNow)
			.When(c => c.ExpirationDate.HasValue)
			.WithMessage("Expiration date must be in the future");

		RuleFor(c => c.ScheduledEnableDate)
			.GreaterThan(DateTime.UtcNow)
			.When(c => c.ScheduledEnableDate.HasValue)
			.WithMessage("Scheduled enable date must be in the future");

		RuleFor(c => c.ScheduledDisableDate)
			.GreaterThan(c => c.ScheduledEnableDate)
			.When(c => c.ScheduledEnableDate.HasValue && c.ScheduledDisableDate.HasValue)
			.WithMessage("Scheduled disable date must be after scheduled enable date");

		RuleFor(c => c.WindowStartTime)
			.LessThan(c => c.WindowEndTime)
			.When(c => c.WindowStartTime.HasValue && c.WindowEndTime.HasValue)
			.WithMessage("Window start time must be before window end time");

		RuleFor(c => c.TimeZone)
			.Must(BeValidTimeZone)
			.When(c => !string.IsNullOrEmpty(c.TimeZone))
			.WithMessage("Invalid time zone identifier");
	}

	private static bool BeValidTimeZone(string? timeZone)
	{
		if (string.IsNullOrEmpty(timeZone)) return true;

		try
		{
			TimeZoneInfo.FindSystemTimeZoneById(timeZone);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
