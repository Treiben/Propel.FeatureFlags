using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;
using System.Text.Json;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public record ToggleFlagRequest(EvaluationMode EvaluationMode, string? Notes);

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
		.WithTags("Feature Flags", "Operations", "Toggle Control", "Dashboard Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();
	}
}

public sealed class ToggleFlagHandler(
		IDashboardRepository repository,
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
		var onOffMode = request.EvaluationMode;
		try
		{
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			// Check if the flag is already in the requested state
			if (flag!.EvaluationOptions.ModeSet.Contains([onOffMode]))
			{
				logger.LogInformation("Feature flag {Key} is already {Status} - no change needed", key, onOffMode);
				return Results.Ok(new FeatureFlagResponse(flag));
			}

			// Store previous state for logging
			var previousModes = flag.EvaluationOptions.ModeSet;

			var notes = request.Notes ?? (onOffMode == EvaluationMode.On ? "Flag enabled" : "Flag disabled");

			// Reset scheduling, time window, and user/tenant access when manually toggling
			var config = flag.EvaluationOptions with
			{
				ModeSet = onOffMode,
				UserAccessControl = new AccessControl(rolloutPercentage: onOffMode == EvaluationMode.On ? 100 : 0),
				TenantAccessControl = new AccessControl(rolloutPercentage: onOffMode == EvaluationMode.On ? 100 : 0),
				OperationalWindow = Knara.UtcStrict.UtcTimeWindow.AlwaysOpen,
				Schedule = Knara.UtcStrict.UtcSchedule.Unscheduled
			};

			// add change history
			var metadata = flag.Administration with
			{
				ChangeHistory =
				[
					.. flag.Administration.ChangeHistory,
					AuditTrail.FlagModified(username: currentUserService.UserName!, notes: notes),
				]
			};
			var flagWithUpdatedModes = flag with { Administration = metadata, EvaluationOptions = config };

			var updatedFlag = await repository.UpdateAsync(flagWithUpdatedModes, cancellationToken);
			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Identifier, cancellationToken);

			var action = Enum.GetName(onOffMode);
			logger.LogInformation("Feature flag {Key} {Action} by {User} (changed from {PreviousStatus}). Reason: {Reason}",
				key, action, currentUserService.UserName, JsonSerializer.Serialize(previousModes), notes);

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

		RuleFor(x => x.Notes)
			.NotEmpty()
			.WithMessage("Reason for toggling the flag is required")
			.MaximumLength(500)
			.WithMessage("Reason cannot exceed 500 characters");
	}
}