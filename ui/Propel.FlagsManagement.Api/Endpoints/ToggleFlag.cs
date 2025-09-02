using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record DisableFlagRequest(string Reason);
public record EnableFlagRequest(string Reason);

public sealed class ToggleFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/enable",
			async (
				string key,
				EnableFlagRequest request,
				ToggleFlagHandler toggleFlagHandler) =>
				{
					return await toggleFlagHandler.HandleAsync(key, FeatureFlagStatus.Enabled, request.Reason);
				})
		.AddEndpointFilter<ValidationFilter<EnableFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("EnableFlag")
		.WithTags("Feature Flags", "Operations", "Toggle Control", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();

		epRoutBuilder.MapPost("/api/feature-flags/{key}/disable",
			async (
				string key,
				DisableFlagRequest request,
				ToggleFlagHandler toggleFlagHandler) =>
			{
				return await toggleFlagHandler.HandleAsync(key, FeatureFlagStatus.Disabled, request.Reason);
			})
		.AddEndpointFilter<ValidationFilter<DisableFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("DisableFlag")
		.WithTags("Feature Flags", "Operations", "Toggle Control", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();
	}
}

public sealed class ToggleFlagHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<ToggleFlagHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(string key, FeatureFlagStatus status, string reason)
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
			// Reset scheduling, time window, and percentage when manually toggling
			flag.ScheduledEnableDate = null;
			flag.ScheduledDisableDate = null;
			flag.WindowStartTime = null;
			flag.WindowEndTime = null;
			flag.WindowDays = null;
			if (status == FeatureFlagStatus.Enabled)
			{
				flag.PercentageEnabled = 100;
			}
			else if (status == FeatureFlagStatus.Disabled)
			{
				flag.PercentageEnabled = 0;
			}

			var updatedFlag = await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			var action = Enum.GetName(status);
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

public sealed class DisableFlagRequestValidator : AbstractValidator<DisableFlagRequest>
{
	public DisableFlagRequestValidator()
	{
		RuleFor(x => x.Reason)
			.NotEmpty()
			.WithMessage("Reason for disabling the flag is required");

		RuleFor(x => x.Reason)
			.MaximumLength(500)
			.WithMessage("Reason cannot exceed 500 characters");
	}
}


