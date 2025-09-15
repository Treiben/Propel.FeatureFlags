using FluentValidation;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FlagsManagement.Api.Endpoints.Dto;
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
				ToggleFlagHandler toggleFlagHandler, 
				CancellationToken cancellationToken) =>
				{
					return await toggleFlagHandler.HandleAsync(key, EvaluationMode.Enabled, request.Reason, cancellationToken);
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
				ToggleFlagHandler toggleFlagHandler, 
				CancellationToken cancellationToken) =>
			{
				return await toggleFlagHandler.HandleAsync(key, EvaluationMode.Disabled, request.Reason, cancellationToken);
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
	public async Task<IResult> HandleAsync(string key, EvaluationMode evaluationMode, string reason,
		CancellationToken cancellationToken)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var flag = await repository.GetAsync(key, cancellationToken);
			if (flag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			// Check if the flag is already in the requested state
			if (flag.ActiveEvaluationModes.ContainsModes([evaluationMode]))
			{
				logger.LogInformation("Feature flag {Key} is already {Status} - no change needed", key, evaluationMode);
				return Results.Ok(new FeatureFlagResponse(flag));
			}

			// Store previous state for logging
			var previousModes = flag.ActiveEvaluationModes.Modes;

			// Update flag
			flag.ActiveEvaluationModes.AddMode(evaluationMode);

			// Reset scheduling, time window, and user/tenant access when manually toggling
			flag.Schedule = FeatureFlags.Domain.ActivationSchedule.Unscheduled;
			flag.OperationalWindow = FeatureFlags.Domain.OperationalWindow.AlwaysOpen;
			flag.UserAccessControl = new AccessControl(rolloutPercentage: evaluationMode == EvaluationMode.Enabled ? 100 : 0);
			flag.TenantAccessControl = new AccessControl(rolloutPercentage: evaluationMode == EvaluationMode.Enabled ? 100 : 0);

			// Update audit record
			flag.LastModified = new FeatureFlags.Core.Audit(timestamp: DateTime.UtcNow, actor: currentUserService.UserName!);

			var updatedFlag = await repository.UpdateAsync(flag, cancellationToken);

			if (cache != null) await cache.RemoveAsync(key, cancellationToken);

			var action = Enum.GetName(evaluationMode);
			logger.LogInformation("Feature flag {Key} {Action} by {User} (changed from {PreviousStatus}). Reason: {Reason}",
				key, action, currentUserService.UserName, previousModes, reason);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest(ex.Message, logger);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			return HttpProblemFactory.ClientClosedRequest(logger);
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


