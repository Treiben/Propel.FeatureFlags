using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record ToggleFlagRequest(EvaluationMode EvaluationMode, string Reason);

public sealed class ToggleFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/toggle",
			async (
				string key,
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				ToggleFlagRequest request,
				ToggleFlagHandler handler,
				CancellationToken cancellationToken) =>
			{
				return await handler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), request, cancellationToken);
			})
		.AddEndpointFilter<ValidationFilter<ToggleFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("ToggleFlag")
		.WithTags("Feature Flags", "Operations", "Toggle Control", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();
	}
}

public sealed class ToggleFlagHandler(
		IFlagManagementRepository repository,
		ICurrentUserService currentUserService,
		IFlagResolverService flagResolver,
		ICacheInvalidationService cacheInvalidationService,
		ILogger<ToggleFlagHandler> logger)
{
	public async Task<IResult> HandleAsync(string key,
		FlagRequestHeaders headers,
		ToggleFlagRequest request,
		CancellationToken cancellationToken)
	{
		var evaluationMode = request.EvaluationMode;
		try
		{
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);

			if (!isValid) return result;

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
			flag.Schedule = ActivationSchedule.Unscheduled;
			flag.OperationalWindow = OperationalWindow.AlwaysOpen;
			flag.UserAccessControl = new AccessControl(rolloutPercentage: evaluationMode == EvaluationMode.Enabled ? 100 : 0);
			flag.TenantAccessControl = new AccessControl(rolloutPercentage: evaluationMode == EvaluationMode.Enabled ? 100 : 0);

			flag!.UpdateAuditTrail(currentUserService.UserName!);

			var updatedFlag = await repository.UpdateAsync(flag, cancellationToken);

			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Key, cancellationToken);

			var action = Enum.GetName(evaluationMode);
			logger.LogInformation("Feature flag {Key} {Action} by {User} (changed from {PreviousStatus}). Reason: {Reason}",
				key, action, currentUserService.UserName, previousModes, request.Reason);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest(ex.Message, logger);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class ToggleFlagRequestValidator : AbstractValidator<ToggleFlagRequest>
{
	public ToggleFlagRequestValidator()
	{
		RuleFor(x => x.EvaluationMode)
			.IsInEnum()
			.WithMessage("EvaluationMode must be a valid value (Enabled or Disabled)");

		RuleFor(x => x.Reason)
			.NotEmpty()
			.WithMessage("Reason for toggling the flag is required")
			.MaximumLength(500)
			.WithMessage("Reason cannot exceed 500 characters");
	}
}