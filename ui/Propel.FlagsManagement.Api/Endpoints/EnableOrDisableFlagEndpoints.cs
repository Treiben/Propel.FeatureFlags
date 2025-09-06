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
					return await toggleFlagHandler.HandleAsync(key, FlagEvaluationMode.Enabled, request.Reason);
				})
		.AddEndpointFilter<ValidationFilter<EnableFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("EnableFlag")
		.WithTags("Feature Flags", "Operations", "Toggle Control", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();

		epRoutBuilder.MapPost("/api/feature-flags/{key}/disable",
			async (
				string key,
				DisableFlagRequest request,
				ToggleFlagHandler toggleFlagHandler) =>
			{
				return await toggleFlagHandler.HandleAsync(key, FlagEvaluationMode.Disabled, request.Reason);
			})
		.AddEndpointFilter<ValidationFilter<DisableFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("DisableFlag")
		.WithTags("Feature Flags", "Operations", "Toggle Control", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();
	}
}

public sealed class ToggleFlagHandler(
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<ToggleFlagHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, FlagEvaluationMode evaluationMode, string reason)
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
			if (flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled, FlagEvaluationMode.Disabled]))
			{
				logger.LogInformation("Feature flag {Key} is already {Status} - no change needed", key, evaluationMode);
				return Results.Ok(new FeatureFlagResponse(flag));
			}

			// Store previous state for logging
			var previousModes = flag.EvaluationModeSet.EvaluationModes;

			// Update flag
			flag.EvaluationModeSet.AddMode(evaluationMode);
			// Reset scheduling, time window, and user/tenant access when manually toggling
			flag.Schedule = FlagActivationSchedule.Unscheduled;
			flag.OperationalWindow = FlagOperationalWindow.AlwaysOpen;
			flag.UserAccess = new FlagUserAccessControl(rolloutPercentage: evaluationMode == FlagEvaluationMode.Enabled ? 100 : 0);
			flag.TenantAccess = new FlagTenantAccessControl(rolloutPercentage: evaluationMode == FlagEvaluationMode.Enabled ? 100 : 0);

			// Update audit record
			flag.AuditRecord = new FlagAuditRecord(flag.AuditRecord.CreatedAt, flag.AuditRecord.CreatedBy, DateTime.UtcNow, currentUserService.UserName!);

			var updatedFlag = await repository.UpdateAsync(flag);

			if (cache != null) await cache.RemoveAsync(key);

			var action = Enum.GetName(evaluationMode);
			logger.LogInformation("Feature flag {Key} {Action} by {User} (changed from {PreviousStatus}). Reason: {Reason}",
				key, action, currentUserService.UserName, previousModes, reason);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
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


