using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record CreateFlagRequest
{
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public FeatureFlagStatus Status { get; set; } = FeatureFlagStatus.Disabled;
	public DateTime? ExpirationDate { get; set; }
	public DateTime? ScheduledEnableDate { get; set; }
	public DateTime? ScheduledDisableDate { get; set; }
	public TimeOnly? WindowStartTime { get; set; }
	public TimeOnly? WindowEndTime { get; set; }
	public string? TimeZone { get; set; }
	public List<DayOfWeek>? WindowDays { get; set; }
	public int PercentageEnabled { get; set; } = 0;
	public List<TargetingRule>? TargetingRules { get; set; }
	public List<string>? EnabledUsers { get; set; }
	public List<string>? DisabledUsers { get; set; }
	public Dictionary<string, object>? Variations { get; set; }
	public string? DefaultVariation { get; set; }
	public Dictionary<string, string>? Tags { get; set; }
	public bool IsPermanent { get; set; } = false;
}

public sealed class CreateFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapPost("/api/feature-flags",
			async (CreateFlagRequest request,
					CreateFlagHandler createFlagHandler) =>
		{
			return await createFlagHandler.HandleAsync(request);
		})
		.AddEndpointFilter<ValidationFilter<CreateFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("CreateFeatureFlag")
		.WithTags("Feature Flags", "CRUD Operations", "Create", "Management Api")
		.Produces<FeatureFlagDto>(StatusCodes.Status201Created);
	}
}

public sealed class CreateFlagHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<CreateFlagHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(CreateFlagRequest request)
	{
		var businessValidationResult = request.ValidateBusinessRules();
		if (!businessValidationResult.IsValid)
		{
			return HttpProblemFactory.ValidationFailed(businessValidationResult.Errors, logger);
		}

		try
		{
			var existingFlag = await repository.GetAsync(request.Key);
			if (existingFlag != null)
			{
				return HttpProblemFactory.Conflict($"A feature flag with the key '{request.Key}' already exists. Please use a different key or update the existing flag.",
					logger);
			}

			var flag = request.Map(currentUserService.UserName!);

			var createdFlag = await repository.CreateAsync(flag);
			await cache.RemoveAsync(flag.Key);

			logger.LogInformation("Feature flag {Key} created successfully by {User}",
				flag.Key, currentUserService.UserName);

			return Results.Created($"/api/flags/{flag.Key}", new FeatureFlagDto(createdFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class CreateFlagRequestValidator : AbstractValidator<CreateFlagRequest>
{
	public CreateFlagRequestValidator()
	{
		RuleFor(c => c.Key)
			.NotEmpty()
			.WithMessage("Feature flag key must be provided");
		RuleFor(c => c.Key)
			.Matches(@"^[a-zA-Z0-9_-]+$")
			.WithMessage("Feature flag key can only contain letters, numbers, hyphens, and underscores.");
		RuleFor(c => c.Key)
			.Length(1, 100)
			.WithMessage("Feature flag key must be between 1 and 100 characters");

		RuleFor(c => c.Name)
			.NotEmpty()
			.WithMessage("Feature flag name must be provided");

		RuleFor(c => c.Name)
			.Length(1, 200)
			.WithMessage("Feature flag name must be between 1 and 200 characters");

		RuleFor(c => c.Description)
			.MaximumLength(1000)
			.When(c => !string.IsNullOrEmpty(c.Description))
			.WithMessage("Feature flag description cannot exceed 1000 characters");

		RuleFor(c => c.PercentageEnabled)
			.InclusiveBetween(0, 100)
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

		RuleFor(c => c.DefaultVariation)
			.Must((request, defaultVariation) => BeValidVariationKey(request.Variations, defaultVariation))
			.When(c => !string.IsNullOrEmpty(c.DefaultVariation) && c.Variations != null)
			.WithMessage("Default variation must exist in the variations dictionary");

		RuleFor(c => c.TargetingRules)
			.Must(HaveValidTargetingRules)
			.When(c => c.TargetingRules != null && c.TargetingRules.Count > 0)
			.WithMessage("All targeting rules must have valid attributes, operators, and values");
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

	private static bool BeValidVariationKey(Dictionary<string, object>? variations, string? defaultVariation)
	{
		if (variations == null || string.IsNullOrEmpty(defaultVariation)) return true;
		return variations.ContainsKey(defaultVariation);
	}

	private static bool HaveValidTargetingRules(List<TargetingRule>? rules)
	{
		if (rules == null) return true;

		return rules.All(rule =>
			!string.IsNullOrEmpty(rule.Attribute) &&
			rule.Values != null &&
			rule.Values.Count > 0 &&
			!string.IsNullOrEmpty(rule.Variation));
	}
}

public static class CreateFlagRequestExtensions
{
	public static BusinessValidationResult ValidateBusinessRules(this CreateFlagRequest request)
	{
		var errors = new Dictionary<string, List<string>>();

		// Validate status-specific requirements
		switch (request.Status)
		{
			case FeatureFlagStatus.Scheduled:
				if (!request.ScheduledEnableDate.HasValue)
					AddError(nameof(request.ScheduledEnableDate), "Scheduled enable date is required when status is 'Scheduled'");
				break;

			case FeatureFlagStatus.TimeWindow:
				if (!request.WindowStartTime.HasValue || !request.WindowEndTime.HasValue)
					AddError(nameof(request.WindowStartTime), "Window start and end times are required when status is 'TimeWindow'");
				if (request.WindowDays == null || request.WindowDays.Count == 0)
					AddError(nameof(request.WindowDays), "Window days are required when status is 'TimeWindow'");
				break;

			case FeatureFlagStatus.Percentage:
				if (request.PercentageEnabled <= 0)
					AddError(nameof(request.PercentageEnabled), "Percentage enabled must be greater than 0 when status is 'Percentage'");
				break;

			case FeatureFlagStatus.UserTargeted:
				if ((request.EnabledUsers == null || request.EnabledUsers.Count == 0) &&
					(request.TargetingRules == null || request.TargetingRules.Count == 0))
					AddError(nameof(request.EnabledUsers), "At least one enabled user or targeting rule is required when status is 'UserTargeted'");
				break;
		}

		// Validate variations consistency
		if (request.Variations != null && request.Variations.Count > 0)
		{
			if (string.IsNullOrEmpty(request.DefaultVariation))
			{
				AddError(nameof(request.DefaultVariation), "Default variation is required when variations are specified");
			}
			else if (!request.Variations.ContainsKey(request.DefaultVariation))
			{
				AddError(nameof(request.DefaultVariation), $"Default variation '{request.DefaultVariation}' must exist in the variations dictionary");
			}

			// Validate targeting rule variations
			if (request.TargetingRules != null)
			{
				var invalidVariations = request.TargetingRules
					.Where(rule => !string.IsNullOrEmpty(rule.Variation) && !request.Variations.ContainsKey(rule.Variation))
					.Select(rule => rule.Variation)
					.Distinct()
					.ToList();

				if (invalidVariations.Count > 0)
				{
					AddError(nameof(request.TargetingRules), $"Targeting rule variations [{string.Join(", ", invalidVariations)}] must exist in the variations dictionary");
				}
			}
		}
		// Validate user lists don't overlap
		if (request.EnabledUsers != null && request.DisabledUsers != null)
		{
			var overlappingUsers = request.EnabledUsers.Intersect(request.DisabledUsers).ToList();
			if (overlappingUsers.Count > 0)
			{
				AddError(nameof(request.EnabledUsers), $"Users cannot be in both enabled and disabled lists: {string.Join(", ", overlappingUsers)}");
			}
		}

		return new BusinessValidationResult { IsValid = errors.Count == 0, Errors = errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()) };

		void AddError(string propertyName, string errorMessage)
		{
			if (!errors.TryGetValue(propertyName, out var value))
			{
				errors[propertyName] = [errorMessage];
			}
			else
			{
				value.Add(errorMessage);
			}
		}
	}

	public static FeatureFlag Map(this CreateFlagRequest source, string creatorUserName)
	{
		return new FeatureFlag
		{
			Key = source.Key,
			Name = source.Name,
			Description = source.Description ?? string.Empty,
			Status = source.Status,
			CreatedBy = creatorUserName,
			UpdatedBy = creatorUserName,
			ExpirationDate = source.ExpirationDate,
			ScheduledEnableDate = source.ScheduledEnableDate,
			ScheduledDisableDate = source.ScheduledDisableDate,
			WindowStartTime = source.WindowStartTime?.ToTimeSpan(),
			WindowEndTime = source.WindowEndTime?.ToTimeSpan(),
			TimeZone = source.TimeZone,
			WindowDays = source.WindowDays,
			PercentageEnabled = source.PercentageEnabled,
			TargetingRules = source.TargetingRules ?? [],
			EnabledUsers = source.EnabledUsers ?? [],
			DisabledUsers = source.DisabledUsers ?? [],
			Variations = source.Variations ?? new() { ["on"] = true, ["off"] = false },
			DefaultVariation = source.DefaultVariation ?? "off",
			Tags = source.Tags ?? [],
			IsPermanent = source.IsPermanent
		};
	}
}