using FeatureRabbit.Flags.Cache;
using FeatureRabbit.Flags.Core;
using FeatureRabbit.Flags.Persistence;
using FeatureRabbit.Management.Api.Endpoints.Shared;
using FluentValidation;

namespace FeatureRabbit.Management.Api.Endpoints;

public record CreateFeatureFlagRequest
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
public sealed class CreateFlag: IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app) {

		app.MapPost("/api/feature-flags",
			async (CreateFeatureFlagRequest request,
					CreateFlagHandler createFlagHandler) =>
		{
			return await createFlagHandler.HandleAsync(request);
		})
		.AddEndpointFilter<ValidationFilter<CreateFeatureFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("CreateFeatureFlag")
		.WithTags("Feature Flags", "Management");
	}

	public sealed class CreateFeatureFlagRequestValidator : AbstractValidator<CreateFeatureFlagRequest>
	{
		public CreateFeatureFlagRequestValidator()
		{
			RuleFor(c => c.Key)
				.NotEmpty()
				.WithMessage("Feature flag key must be provided");
			RuleFor(c => c.Key)
				.Matches(@"^[a-zA-Z0-9_-]+$")
				.WithMessage("Feature flag key can only contain letters, numbers, hyphens, and underscores.");
			RuleFor(c => c.Name)
				.NotEmpty()
				.MaximumLength(200)
				.WithMessage("Stop name length must be greater than 0 and less than 200.");
			RuleFor(c => c.PercentageEnabled)
				.Must(p => p >= 0 && p <= 100)
				.WithMessage("Feature flag rollout percentage must be between 0 and 100.");
		}
	}
}

public sealed class CreateFlagHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<CreateFlagHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(CreateFeatureFlagRequest request)
	{
		try
		{
			var existingFlag = await repository.GetAsync(request.Key);
			if (existingFlag != null)
				return Results.Conflict($"Feature flag '{request.Key}' already exists");
			var flag = new FeatureFlag
			{
				Key = request.Key,
				Name = request.Name,
				Description = request.Description ?? string.Empty,
				Status = request.Status,
				CreatedBy = currentUserService.UserName,
				UpdatedBy = currentUserService.UserName,
				ExpirationDate = request.ExpirationDate,
				ScheduledEnableDate = request.ScheduledEnableDate,
				ScheduledDisableDate = request.ScheduledDisableDate,
				WindowStartTime = request.WindowStartTime?.ToTimeSpan(),
				WindowEndTime = request.WindowEndTime?.ToTimeSpan(),
				TimeZone = request.TimeZone,
				WindowDays = request.WindowDays,
				PercentageEnabled = request.PercentageEnabled,
				TargetingRules = request.TargetingRules ?? new(),
				EnabledUsers = request.EnabledUsers ?? new(),
				DisabledUsers = request.DisabledUsers ?? new(),
				Variations = request.Variations ?? new() { ["on"] = true, ["off"] = false },
				DefaultVariation = request.DefaultVariation ?? "off",
				Tags = request.Tags ?? new(),
				IsPermanent = request.IsPermanent
			};
			await repository.CreateAsync(flag);
			await cache.RemoveAsync(flag.Key);
			logger.LogInformation("Feature flag {Key} created by {User}", flag.Key, currentUserService.UserName);
			return Results.Created($"/api/flags/{flag.Key}", new FeatureFlagDto(flag));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error creating feature flag");
			return Results.StatusCode(500);
		}
	}
}
