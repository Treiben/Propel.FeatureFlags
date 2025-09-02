using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record UpdateFlagRequest
{
	public string? Name { get; set; }
	public string? Description { get; set; }
	public DateTime? ExpirationDate { get; set; }
	public List<TargetingRule>? TargetingRules { get; set; }
	public List<string>? EnabledUsers { get; set; }
	public List<string>? DisabledUsers { get; set; }
	public Dictionary<string, object>? Variations { get; set; }
	public string? DefaultVariation { get; set; }
	public Dictionary<string, string>? Tags { get; set; }
	public bool? IsPermanent { get; set; }
}

public sealed class UpdateFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPut("/api/feature-flags/{key}",
			async (string key, 
					UpdateFlagRequest request,
					UpdateFlagHandler handler) =>
		{
			return await handler.HandleAsync(key, request);
		})
		.AddEndpointFilter<ValidationFilter<UpdateFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("UpdateFeatureFlag")
		.WithTags("Feature Flags", "CRUD Operations", "Update", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();
	}
}

public sealed class UpdateFlagHandler(
	CurrentUserService userService,
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<UpdateFlagHandler> logger)
{
	public async Task<IResult> HandleAsync(string key, UpdateFlagRequest request)
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

public static class UpdateFlagRequestExtensions
{
	public static BusinessValidationResult ValidateBusinessRules(this UpdateFlagRequest request, FeatureFlag existingFlag)
	{
		var errors = new Dictionary<string, List<string>>();

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

	public static void Map(this UpdateFlagRequest source, FeatureFlag dest)
	{
		// Update only non-null properties from the request
		if (source.Name != null) dest.Name = source.Name;
		if (source.Description != null) dest.Description = source.Description;
		if (source.ExpirationDate.HasValue) dest.ExpirationDate = source.ExpirationDate;
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

public sealed class UpdateFlagRequestValidator : AbstractValidator<UpdateFlagRequest>
{
	public UpdateFlagRequestValidator()
	{
		RuleFor(c => c.Name)
			.MaximumLength(200)
			.When(c => !string.IsNullOrEmpty(c.Name))
			.WithMessage("Feature flag name must be between 1 and 200 characters");

		RuleFor(c => c.Description)
			.MaximumLength(1000)
			.When(c => !string.IsNullOrEmpty(c.Description))
			.WithMessage("Feature flag description cannot exceed 1000 characters");

		RuleFor(c => c.ExpirationDate)
			.GreaterThan(DateTime.UtcNow)
			.When(c => c.ExpirationDate.HasValue)
			.WithMessage("Expiration date must be in the future");
	}
}
