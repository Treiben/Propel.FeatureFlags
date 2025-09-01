using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record EnableFlagRequest(string Reason);

public sealed class EnableFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/enable",
			async (
				string key,
				EnableFlagRequest request,
				EnableFlagHandler toggleFlagHandler) =>
		{
			return await toggleFlagHandler.HandleAsync(key, request.Reason);
		})
		.AddEndpointFilter<ValidationFilter<EnableFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("EnableFlag")
		.WithTags("Feature Flags", "Operations", "Toggle Control", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();
	}
}

public sealed class EnableFlagRequestValidator : AbstractValidator<EnableFlagRequest>
{
	public EnableFlagRequestValidator()
	{
		RuleFor(x => x.Reason)
			.NotEmpty()
			.WithMessage("Reason for enabling the flag is required");

		RuleFor(x => x.Reason)
			.MaximumLength(500)
			.WithMessage("Reason cannot exceed 500 characters");
	}
}

public sealed class EnableFlagHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<EnableFlagHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(string key, string reason)
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

			var status = FeatureFlagStatus.Enabled;
			// Check if the flag is already in the requested state
			if (flag.Status == status)
			{
				logger.LogInformation("Feature flag {Key} is already {Status} - no change needed", key, status);
				return Results.Ok(new FeatureFlagDto(flag));
			}

			// Store previous state for logging
			var previousStatus = flag.Status;

			// Update flag
			flag.Status = status;
			flag.UpdatedBy = currentUserService.UserName!;

			// Reset scheduling and percentage when manually toggling
			flag.ScheduledEnableDate = null;
			flag.ScheduledDisableDate = null;
			flag.PercentageEnabled = 100;

			var updatedFlag = await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			var action = "enabled";
			logger.LogInformation("Feature flag {Key} {Action} by {User} (changed from {PreviousStatus}). Reason: {Reason}",
				key, action, currentUserService.UserName, previousStatus, reason);

			return Results.Ok(new FeatureFlagDto(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}


